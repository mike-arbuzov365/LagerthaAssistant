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

  it('uses initDataUnsafe user when present', () => {
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

    host?.ready()
    expect(ready).toHaveBeenCalledOnce()
    expect(expand).toHaveBeenCalledOnce()
    expect(requestFullscreen).toHaveBeenCalledOnce()

    const cleanup = host?.enableBestEffortFullscreen()
    window.dispatchEvent(new Event('pointerdown'))
    expect(expand).toHaveBeenCalledTimes(2)
    expect(requestFullscreen).toHaveBeenCalledTimes(2)
    cleanup?.()
  })

  it('falls back to parsing initData user payload when initDataUnsafe user is missing', () => {
    const encodedUser = encodeURIComponent(JSON.stringify({ id: 98765, language_code: 'en' }))

    window.Telegram = {
      WebApp: {
        initData: `query_id=q1&user=${encodedUser}&auth_date=1&hash=h`,
        initDataUnsafe: {},
        colorScheme: 'light',
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
  })

  it('ignores fullscreen errors from older Telegram clients', () => {
    const ready = vi.fn()
    const expand = vi.fn()
    const requestFullscreen = vi.fn(() => {
      throw new Error('unsupported')
    })

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
        contentSafeAreaInsets: { top: 0 },
        ready,
        expand,
        requestFullscreen,
      },
    }

    const host = createTelegramHost()

    expect(() => host?.ready()).not.toThrow()
    expect(ready).toHaveBeenCalledOnce()
    expect(expand).toHaveBeenCalledOnce()
    expect(requestFullscreen).toHaveBeenCalledOnce()
  })

  it('cleans up best-effort fullscreen listeners after first interaction', () => {
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
        contentSafeAreaInsets: { top: 0 },
        ready,
        expand,
        requestFullscreen,
      },
    }

    const host = createTelegramHost()
    const cleanup = host?.enableBestEffortFullscreen()

    window.dispatchEvent(new Event('pointerdown'))
    window.dispatchEvent(new Event('pointerdown'))

    expect(expand).toHaveBeenCalledOnce()
    expect(requestFullscreen).toHaveBeenCalledOnce()

    cleanup?.()
  })
})
