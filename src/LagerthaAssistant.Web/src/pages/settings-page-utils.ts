import type { AppLocale } from '../lib/locale'
import type { AppThemeMode } from '../lib/theme'
import { resolveTelegramBridge } from '../lib/telegramBridge'

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

export function resolveTelegramMiniAppBridge(): TelegramMiniAppBridgeWebApp | undefined {
  return resolveTelegramBridge() as TelegramMiniAppBridgeWebApp | undefined
}

export async function waitForTelegramMiniAppBridge(
  timeoutMs = 480,
  intervalMs = 40,
): Promise<TelegramMiniAppBridgeWebApp | undefined> {
  const start = Date.now()
  let bridge = resolveTelegramMiniAppBridge()

  while (!bridge && Date.now() - start < timeoutMs) {
    await new Promise<void>((resolve) => window.setTimeout(resolve, intervalMs))
    bridge = resolveTelegramMiniAppBridge()
  }

  return bridge
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
  if (webApp) {
    if (enabled) {
      webApp.enableClosingConfirmation?.()
    } else {
      webApp.disableClosingConfirmation?.()
    }

    try {
      webApp.isClosingConfirmationEnabled = enabled
    } catch {
      // no-op
    }
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
    // Не вимикаємо confirmation у cleanup, інакше Telegram може проковтнути close prompt під час unmount.
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
