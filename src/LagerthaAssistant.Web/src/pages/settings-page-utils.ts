import type { AppLocale } from '../lib/locale'

export interface PersistedSnapshot {
  locale: AppLocale
  saveMode: string
  storageMode: string
  aiProvider: string
  aiModel: string
}

export interface SettingsDraftState {
  locale: AppLocale
  saveMode: string
  storageMode: string
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
  sendData?: (data: string) => void
  close?: () => void
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
    || draft.aiProvider !== snapshot.aiProvider
    || draft.aiModel !== snapshot.aiModel
    || draft.removeStoredKeyRequested
    || draft.apiKeyDraft.trim().length > 0
  )
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
}

export function syncTelegramClosingConfirmation(
  webApp: TelegramClosingConfirmationWebApp | undefined,
  enabled: boolean,
): () => void {
  applyTelegramClosingConfirmation(webApp, enabled)

  return () => {
    // Intentionally no-op.
    // During Telegram Mini App close, React unmount may happen before Telegram processes
    // closing behavior. Disabling confirmation in cleanup can suppress the close prompt.
  }
}

export function sendTelegramMiniAppSettingsSaved(
  webApp: TelegramMiniAppBridgeWebApp | undefined,
  locale: AppLocale,
): boolean {
  if (!webApp?.sendData) {
    return false
  }

  try {
    webApp.sendData(JSON.stringify({
      type: 'settings_saved',
      locale,
    }))
    return true
  } catch {
    return false
  }
}

export function closeTelegramMiniApp(webApp: TelegramMiniAppBridgeWebApp | undefined): boolean {
  if (!webApp?.close) {
    return false
  }

  try {
    webApp.close()
    return true
  } catch {
    return false
  }
}
