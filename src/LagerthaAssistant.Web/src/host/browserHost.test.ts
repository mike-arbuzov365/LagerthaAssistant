import { describe, expect, it } from 'vitest'
import { createBrowserHost } from './browserHost'

describe('createBrowserHost', () => {
  it('returns non-telegram host with safe defaults', () => {
    const host = createBrowserHost()
    expect(host.isTelegram).toBe(false)
    expect(host.safeAreaTop).toBe(0)
    expect(host.initData).toBe('')
    expect(host.theme).toBe('light')
  })
})
