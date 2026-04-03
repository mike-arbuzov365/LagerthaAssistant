import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTelegramHost } from './telegramHost'

const originalTelegram = window.Telegram

function resetTelegramGlobal() {
  if (typeof originalTelegram === 'undefined') {
    delete window.Telegram
    return
  }

  window.Telegram = originalTelegram
}

describe('createTelegramHost', () => {
  afterEach(() => {
    resetTelegramGlobal()
  })

  it('returns null when Telegram WebApp is not available', () => {
    delete window.Telegram
    expect(createTelegramHost()).toBeNull()
  })

  it('uses initDataUnsafe user when present and expands on ready', () => {
    const ready = vi.fn()
    const expand = vi.fn()
    const requestFullscreen = vi.fn()

    window.Telegram = {
      WebApp: {
        initData: 'auth_date=1',
        initDataUnsafe: {
          user: {
            id: 123456,
            language_code: 'uk',
          },
        },
        colorScheme: 'dark',
        platform: 'tdesktop',
        contentSafeAreaInsets: { top: 8 },
        ready,
        expand,
        requestFullscreen,
      },
    }

    const host = createTelegramHost()
    expect(host).not.toBeNull()
    expect(host?.userId).toBe('123456')
    expect(host?.conversationId).toBe('123456')
    expect(host?.userLanguageCode).toBe('uk')
    expect(host?.theme).toBe('dark')
    expect(host?.platform).toBe('desktop')

    host?.ready()
    expect(ready).toHaveBeenCalledOnce()
    expect(expand).toHaveBeenCalledOnce()
    expect(requestFullscreen).not.toHaveBeenCalled()
  })

  it('falls back to parsing initData user payload when initDataUnsafe user is missing', () => {
    const encodedUser = encodeURIComponent(JSON.stringify({ id: 98765, language_code: 'en' }))

    window.Telegram = {
      WebApp: {
        initData: `query_id=q1&user=${encodedUser}&auth_date=1&hash=h`,
        initDataUnsafe: {},
        colorScheme: 'light',
        platform: 'android',
        contentSafeAreaInsets: { top: 0 },
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
  })

  it('returns null when initData is missing even if Telegram global exists', () => {
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
    const ready = vi.fn()
    const expand = vi.fn()
    const requestFullscreen = vi.fn()

    window.Telegram = {
      WebApp: {
        initData: 'auth_date=1',
        initDataUnsafe: {
          user: {
            id: 123456,
            language_code: 'uk',
          },
        },
        colorScheme: 'light',
        platform: 'ios',
        contentSafeAreaInsets: { top: 0 },
        ready,
        expand,
        requestFullscreen,
      },
    }

    const host = createTelegramHost()
    host?.ready()

    expect(ready).toHaveBeenCalledOnce()
    expect(expand).toHaveBeenCalledOnce()
    expect(requestFullscreen).toHaveBeenCalledOnce()
  })
})
