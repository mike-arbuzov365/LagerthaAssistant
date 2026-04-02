import { describe, expect, it, vi } from 'vitest'
import {
  applyTelegramClosingConfirmation,
  buildUnsavedChangesPrompt,
  closeTelegramMiniApp,
  hasUnsavedSettingsChanges,
  normalizeLocaleFromPreference,
  type PersistedSnapshot,
  type SettingsDraftState,
  syncTelegramClosingConfirmation,
} from './settings-page-utils'

describe('normalizeLocaleFromPreference', () => {
  it('normalizes english-like values to en', () => {
    expect(normalizeLocaleFromPreference('en')).toBe('en')
    expect(normalizeLocaleFromPreference('en-US')).toBe('en')
    expect(normalizeLocaleFromPreference('fr')).toBe('en')
    expect(normalizeLocaleFromPreference(null)).toBe('en')
  })

  it('normalizes ukrainian/russian/belarusian-like values to uk', () => {
    expect(normalizeLocaleFromPreference('uk')).toBe('uk')
    expect(normalizeLocaleFromPreference('uk-UA')).toBe('uk')
    expect(normalizeLocaleFromPreference('ua')).toBe('uk')
    expect(normalizeLocaleFromPreference('ru')).toBe('uk')
    expect(normalizeLocaleFromPreference('be')).toBe('uk')
  })
})

describe('hasUnsavedSettingsChanges', () => {
  const snapshot: PersistedSnapshot = {
    locale: 'uk',
    saveMode: 'ask',
    storageMode: 'graph',
    aiProvider: 'openai',
    aiModel: 'gpt-4.1-mini',
  }

  function draft(overrides?: Partial<SettingsDraftState>): SettingsDraftState {
    return {
      locale: 'uk',
      saveMode: 'ask',
      storageMode: 'graph',
      aiProvider: 'openai',
      aiModel: 'gpt-4.1-mini',
      apiKeyDraft: '',
      removeStoredKeyRequested: false,
      ...overrides,
    }
  }

  it('returns false when snapshot is missing', () => {
    expect(hasUnsavedSettingsChanges(null, draft())).toBe(false)
  })

  it('returns false when draft equals snapshot', () => {
    expect(hasUnsavedSettingsChanges(snapshot, draft())).toBe(false)
  })

  it('returns true when locale changes', () => {
    expect(hasUnsavedSettingsChanges(snapshot, draft({ locale: 'en' }))).toBe(true)
  })

  it('returns true when api key was entered', () => {
    expect(hasUnsavedSettingsChanges(snapshot, draft({ apiKeyDraft: 'secret-key' }))).toBe(true)
  })

  it('returns true when remove key checkbox is selected', () => {
    expect(hasUnsavedSettingsChanges(snapshot, draft({ removeStoredKeyRequested: true }))).toBe(true)
  })
})

describe('applyTelegramClosingConfirmation', () => {
  it('enables and sets compatibility flag', () => {
    const webApp = {
      enableClosingConfirmation: vi.fn(),
      disableClosingConfirmation: vi.fn(),
      isClosingConfirmationEnabled: false,
    }

    applyTelegramClosingConfirmation(webApp, true)

    expect(webApp.enableClosingConfirmation).toHaveBeenCalledOnce()
    expect(webApp.disableClosingConfirmation).not.toHaveBeenCalled()
    expect(webApp.isClosingConfirmationEnabled).toBe(true)
  })

  it('disables and sets compatibility flag', () => {
    const webApp = {
      enableClosingConfirmation: vi.fn(),
      disableClosingConfirmation: vi.fn(),
      isClosingConfirmationEnabled: true,
    }

    applyTelegramClosingConfirmation(webApp, false)

    expect(webApp.disableClosingConfirmation).toHaveBeenCalledOnce()
    expect(webApp.enableClosingConfirmation).not.toHaveBeenCalled()
    expect(webApp.isClosingConfirmationEnabled).toBe(false)
  })
})

describe('buildUnsavedChangesPrompt', () => {
  it('returns localized unsaved prompt copy', () => {
    expect(buildUnsavedChangesPrompt('en')).toContain('unsaved changes')
    expect(buildUnsavedChangesPrompt('uk')).toContain('незбережені зміни')
  })
})

describe('syncTelegramClosingConfirmation', () => {
  it('reapplies confirmation state after a short delay', () => {
    vi.useFakeTimers()
    const webApp = {
      enableClosingConfirmation: vi.fn(),
      disableClosingConfirmation: vi.fn(),
      isClosingConfirmationEnabled: false,
    }

    syncTelegramClosingConfirmation(webApp, true)
    vi.runAllTimers()

    expect(webApp.enableClosingConfirmation).toHaveBeenCalledTimes(3)
    vi.useRealTimers()
  })

  it('does not disable confirmation during cleanup after enabling', () => {
    vi.useFakeTimers()
    const webApp = {
      enableClosingConfirmation: vi.fn(),
      disableClosingConfirmation: vi.fn(),
      isClosingConfirmationEnabled: false,
    }

    const cleanup = syncTelegramClosingConfirmation(webApp, true)
    cleanup()

    expect(webApp.enableClosingConfirmation).toHaveBeenCalledOnce()
    expect(webApp.disableClosingConfirmation).not.toHaveBeenCalled()
    expect(webApp.isClosingConfirmationEnabled).toBe(true)
    vi.useRealTimers()
  })

  it('applies disabled state when enabled=false', () => {
    vi.useFakeTimers()
    const webApp = {
      enableClosingConfirmation: vi.fn(),
      disableClosingConfirmation: vi.fn(),
      isClosingConfirmationEnabled: true,
    }

    syncTelegramClosingConfirmation(webApp, false)
    vi.runAllTimers()

    expect(webApp.disableClosingConfirmation).toHaveBeenCalledTimes(3)
    expect(webApp.isClosingConfirmationEnabled).toBe(false)
    vi.useRealTimers()
  })
})

describe('closeTelegramMiniApp', () => {
  it('closes mini app when close API is available', () => {
    const close = vi.fn()
    const result = closeTelegramMiniApp({ close })
    expect(result).toBe(true)
    expect(close).toHaveBeenCalledOnce()
  })

  it('returns false when close API is unavailable', () => {
    expect(closeTelegramMiniApp({})).toBe(false)
  })
})
