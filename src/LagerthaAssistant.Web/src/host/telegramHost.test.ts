import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTelegramHost } from './telegramHost'

const originalTelegram = window.Telegram
const originalHref = window.location.href

function resetTelegramGlobal() {
  if (typeof originalTelegram === 'undefined') {
    delete window.Telegram
    return
  }

  window.Telegram = originalTelegram
}

describe('createTelegramHost', () => {
  afterEach(() => {
    vi.useRealTimers()
    resetTelegramGlobal()
    window.history.pushState({}, '', originalHref)
  })

  it('returns null when Telegram launch context is not available', () => {
    delete window.Telegram
    expect(createTelegramHost()).toBeNull()
  })

  it('uses initDataUnsafe user when present and keeps desktop launch compact on ready', () => {
    vi.useFakeTimers()
    const ready = vi.fn()
    const expand = vi.fn()
    const requestFullscreen = vi.fn()
    const hideBackButton = vi.fn()
    const hideMainButton = vi.fn()
    const hideSecondaryButton = vi.fn()
    const hideSettingsButton = vi.fn()

    window.Telegram = {
      WebApp: {
        initData: 'auth_date=1&hash=h',
        initDataUnsafe: {
          user: {
            id: 123456,
            language_code: 'uk',
          },
        },
        colorScheme: 'dark',
        platform: 'tdesktop',
        safeAreaInset: { top: 12, bottom: 10 },
        contentSafeAreaInset: { top: 28, bottom: 16 },
        ready,
        expand,
        requestFullscreen,
        BackButton: { hide: hideBackButton },
        MainButton: { hide: hideMainButton },
        SecondaryButton: { hide: hideSecondaryButton },
        SettingsButton: { hide: hideSettingsButton },
      },
    }

    const host = createTelegramHost()
    expect(host).not.toBeNull()
    expect(host?.source).toBe('telegram-webapp')
    expect(host?.userId).toBe('123456')
    expect(host?.conversationId).toBe('123456')
    expect(host?.userLanguageCode).toBe('uk')
    expect(host?.theme).toBe('dark')
    expect(host?.platform).toBe('desktop')
    expect(host?.safeAreaTop).toBe(28)

    host?.ready()
    vi.runAllTimers()

    expect(ready).toHaveBeenCalled()
    expect(expand).not.toHaveBeenCalled()
    expect(requestFullscreen).not.toHaveBeenCalled()
    expect(hideBackButton).toHaveBeenCalled()
    expect(hideMainButton).toHaveBeenCalled()
    expect(hideSecondaryButton).toHaveBeenCalled()
    expect(hideSettingsButton).toHaveBeenCalled()
  })

  it('falls back to parsing initData user payload when initDataUnsafe user is missing', () => {
    const encodedUser = encodeURIComponent(JSON.stringify({ id: 98765, language_code: 'en' }))

    window.Telegram = {
      WebApp: {
        initData: `query_id=q1&user=${encodedUser}&auth_date=1&hash=h`,
        initDataUnsafe: {},
        colorScheme: 'light',
        platform: 'android',
        contentSafeAreaInset: { top: 22, bottom: 12 },
        ready: vi.fn(),
        expand: vi.fn(),
        requestFullscreen: vi.fn(),
      },
    }

    const host = createTelegramHost()
    expect(host).not.toBeNull()
    expect(host?.userId).toBe('98765')
    expect(host?.conversationId).toBe('98765')
    expect(host?.userLanguageCode).toBe('en')
    expect(host?.theme).toBe('light')
    expect(host?.platform).toBe('android')
    expect(host?.safeAreaTop).toBe(22)
  })

  it('uses Telegram launch params when the bridge is not ready yet', () => {
    delete window.Telegram

    const encodedUser = encodeURIComponent(JSON.stringify({ id: 445566, language_code: 'en' }))
    const initData = encodeURIComponent(`query_id=q1&user=${encodedUser}&auth_date=1&hash=h`)
    const themeParams = encodeURIComponent(JSON.stringify({ bg_color: '#17212b' }))
    window.history.pushState({}, '', `/miniapp/settings?tgWebAppData=${initData}&tgWebAppPlatform=ios&tgWebAppThemeParams=${themeParams}`)

    const host = createTelegramHost()
    expect(host).not.toBeNull()
    expect(host?.source).toBe('telegram-launch-params')
    expect(host?.isTelegram).toBe(true)
    expect(host?.userId).toBe('445566')
    expect(host?.platform).toBe('ios')
    expect(host?.theme).toBe('dark')
    expect(host?.initData).toContain('query_id=q1')
  })

  it('returns null when initData is missing and no launch params exist', () => {
    window.Telegram = {
      WebApp: {
        initDataUnsafe: {
          user: {
            id: 123456,
            language_code: 'uk',
          },
        },
        ready: vi.fn(),
        expand: vi.fn(),
      },
    }

    expect(createTelegramHost()).toBeNull()
  })

  it('requests fullscreen for mobile platforms during ready', () => {
    vi.useFakeTimers()
    const ready = vi.fn()
    const expand = vi.fn()
    const requestFullscreen = vi.fn()
    const hideBackButton = vi.fn()
    const hideMainButton = vi.fn()
    const hideSecondaryButton = vi.fn()
    const hideSettingsButton = vi.fn()

    window.Telegram = {
      WebApp: {
        initData: 'auth_date=1&hash=h',
        initDataUnsafe: {
          user: {
            id: 123456,
            language_code: 'uk',
          },
        },
        colorScheme: 'light',
        platform: 'ios',
        contentSafeAreaInset: { top: 24, bottom: 18 },
        ready,
        expand,
        requestFullscreen,
        BackButton: { hide: hideBackButton },
        MainButton: { hide: hideMainButton },
        SecondaryButton: { hide: hideSecondaryButton },
        SettingsButton: { hide: hideSettingsButton },
      },
    }

    const host = createTelegramHost()
    expect(host?.safeAreaTop).toBe(24)
    host?.ready()
    vi.runAllTimers()

    expect(ready).toHaveBeenCalled()
    expect(expand).toHaveBeenCalled()
    expect(requestFullscreen).toHaveBeenCalled()
    expect(hideBackButton).toHaveBeenCalled()
    expect(hideMainButton).toHaveBeenCalled()
    expect(hideSecondaryButton).toHaveBeenCalled()
    expect(hideSettingsButton).toHaveBeenCalled()
  })
})
