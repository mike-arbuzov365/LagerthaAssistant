import { describe, expect, it, vi } from 'vitest'
import {
  applyTelegramClosingConfirmation,
  buildTelegramMiniAppSettingsCommitPayload,
  closeTelegramMiniApp,
  hasUnsavedSettingsChanges,
  normalizeLocaleFromPreference,
  sendTelegramMiniAppSettingsCommit,
  sendTelegramMiniAppSettingsSaved,
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

describe('syncTelegramClosingConfirmation', () => {
  it('does not disable confirmation during cleanup after enabling', () => {
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
  })

  it('applies disabled state when enabled=false', () => {
    const webApp = {
      enableClosingConfirmation: vi.fn(),
      disableClosingConfirmation: vi.fn(),
      isClosingConfirmationEnabled: true,
    }

    syncTelegramClosingConfirmation(webApp, false)

    expect(webApp.disableClosingConfirmation).toHaveBeenCalledOnce()
    expect(webApp.isClosingConfirmationEnabled).toBe(false)
  })
})

describe('sendTelegramMiniAppSettingsSaved', () => {
  it('sends settings_saved payload with locale', () => {
    const sendData = vi.fn()

    const result = sendTelegramMiniAppSettingsSaved({ sendData }, 'en')

    expect(result).toBe(true)
    expect(sendData).toHaveBeenCalledOnce()
    expect(sendData).toHaveBeenCalledWith('{"type":"settings_saved","locale":"en"}')
  })

  it('returns false when sendData is unavailable', () => {
    expect(sendTelegramMiniAppSettingsSaved({}, 'uk')).toBe(false)
  })
})

describe('buildTelegramMiniAppSettingsCommitPayload', () => {
  it('builds a single atomic commit payload and omits empty api keys', () => {
    const payload = buildTelegramMiniAppSettingsCommitPayload({
      locale: 'en',
      saveMode: 'auto',
      storageMode: 'graph',
      aiProvider: 'claude',
      aiModel: 'claude-3-7-sonnet',
      apiKey: '   ',
      removeStoredKey: true,
    })

    expect(payload).toBe(
      '{"type":"settings_commit","locale":"en","saveMode":"auto","storageMode":"graph","aiProvider":"claude","aiModel":"claude-3-7-sonnet","removeStoredKey":true}',
    )
  })
})

describe('sendTelegramMiniAppSettingsCommit', () => {
  it('sends the full settings_commit payload', () => {
    const sendData = vi.fn()

    const result = sendTelegramMiniAppSettingsCommit(
      { sendData },
      {
        locale: 'uk',
        saveMode: 'ask',
        storageMode: 'graph',
        aiProvider: 'openai',
        aiModel: 'gpt-4.1-mini',
        apiKey: 'secret',
        removeStoredKey: false,
      },
    )

    expect(result).toBe(true)
    expect(sendData).toHaveBeenCalledWith(
      '{"type":"settings_commit","locale":"uk","saveMode":"ask","storageMode":"graph","aiProvider":"openai","aiModel":"gpt-4.1-mini","apiKey":"secret"}',
    )
  })

  it('returns false when sendData is unavailable', () => {
    expect(
      sendTelegramMiniAppSettingsCommit(
        {},
        {
          locale: 'uk',
          saveMode: 'ask',
          storageMode: 'graph',
          aiProvider: 'openai',
          aiModel: 'gpt-4.1-mini',
        },
      ),
    ).toBe(false)
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
