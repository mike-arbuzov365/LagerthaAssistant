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
      },
    }

    const host = createTelegramHost()
    expect(host).not.toBeNull()
    expect(host?.userId).toBe('123456')
    expect(host?.userLanguageCode).toBe('uk')
    expect(host?.theme).toBe('dark')

    host?.ready()
    expect(ready).toHaveBeenCalledOnce()
    expect(expand).toHaveBeenCalledOnce()
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
      },
    }

    const host = createTelegramHost()
    expect(host).not.toBeNull()
    expect(host?.userId).toBe('98765')
    expect(host?.userLanguageCode).toBe('en')
    expect(host?.theme).toBe('light')
  })
})

