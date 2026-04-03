import type { AppLocale } from '../lib/locale'
import type { AppThemeMode } from '../lib/theme'

type TelegramWebViewWindow = Window & {
  Telegram?: {
    WebView?: {
      postEvent?: (eventType: string, callback?: boolean, eventData?: Record<string, unknown>) => void
    }
  }
}

export interface PersistedSnapshot {
  locale: AppLocale
  saveMode: string
  storageMode: string
  themeMode: AppThemeMode
  aiProvider: string
  aiModel: string
}

export interface SettingsDraftState {
  locale: AppLocale
  saveMode: string
  storageMode: string
  themeMode: AppThemeMode
  aiProvider: string
  aiModel: string
  apiKeyDraft: string
  removeStoredKeyRequested: boolean
}

export type TelegramClosingConfirmationWebApp = {
  enableClosingConfirmation?: () => void
  disableClosingConfirmation?: () => void
  isClosingConfirmationEnabled?: boolean
}

export type TelegramMiniAppBridgeWebApp = TelegramClosingConfirmationWebApp & {
  close?: (options?: { return_back?: boolean }) => void
}
export function normalizeLocaleFromPreference(value: string | null | undefined): AppLocale {
  const normalized = value?.trim().toLowerCase() ?? ''

  if (normalized.startsWith('uk') || normalized.startsWith('ua') || normalized.startsWith('ru') || normalized.startsWith('be')) {
    return 'uk'
  }

  return 'en'
}

export function hasUnsavedSettingsChanges(
  snapshot: PersistedSnapshot | null,
  draft: SettingsDraftState,
): boolean {
  if (!snapshot) {
    return false
  }

  return (
    draft.locale !== snapshot.locale
    || draft.saveMode !== snapshot.saveMode
    || draft.storageMode !== snapshot.storageMode
    || draft.themeMode !== snapshot.themeMode
    || draft.aiProvider !== snapshot.aiProvider
    || draft.aiModel !== snapshot.aiModel
    || draft.removeStoredKeyRequested
    || draft.apiKeyDraft.trim().length > 0
  )
}

function postTelegramWebViewEvent(
  eventType: string,
  eventData?: Record<string, unknown>,
): boolean {
  const postEvent = (window as TelegramWebViewWindow).Telegram?.WebView?.postEvent
  if (typeof postEvent !== 'function') {
    return false
  }

  try {
    postEvent(eventType, false, eventData)
    return true
  } catch {
    return false
  }
}

export function applyTelegramClosingConfirmation(
  webApp: TelegramClosingConfirmationWebApp | undefined,
  enabled: boolean,
) {
  if (!webApp) {
    return
  }

  if (enabled) {
    webApp.enableClosingConfirmation?.()
  } else {
    webApp.disableClosingConfirmation?.()
  }

  // Compatibility fallback for Telegram clients that expose only flag-based behavior.
  try {
    webApp.isClosingConfirmationEnabled = enabled
  } catch {
    // no-op
  }

  postTelegramWebViewEvent('web_app_setup_closing_behavior', {
    need_confirmation: enabled,
  })
}

export function buildUnsavedChangesPrompt(locale: AppLocale): string {
  return locale === 'en'
    ? 'You have unsaved changes. Save or discard them before leaving settings.'
    : 'У вас є незбережені зміни. Збережіть або скасуйте їх перед виходом із налаштувань.'
}

export function syncTelegramClosingConfirmation(
  webApp: TelegramClosingConfirmationWebApp | undefined,
  enabled: boolean,
): () => void {
  applyTelegramClosingConfirmation(webApp, enabled)

  const timerIds = [0, 120].map((delay) => window.setTimeout(() => {
    applyTelegramClosingConfirmation(webApp, enabled)
  }, delay))

  return () => {
    timerIds.forEach((timerId) => window.clearTimeout(timerId))
    // Intentionally no-op.
    // During Telegram Mini App close, React unmount may happen before Telegram processes
    // closing behavior. Disabling confirmation in cleanup can suppress the close prompt.
  }
}

export function closeTelegramMiniApp(webApp: TelegramMiniAppBridgeWebApp | undefined): boolean {
  let closed = false
  try {
    webApp?.close?.({ return_back: true })
    closed = Boolean(webApp?.close)
  } catch {
    // no-op
  }

  return postTelegramWebViewEvent('web_app_close', { return_back: true }) || closed
}
