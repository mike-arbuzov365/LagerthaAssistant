import type { HostContext } from './types'

export function createBrowserHost(): HostContext {
  return {
    isTelegram: false,
    theme: 'light',
    platform: 'desktop',
    safeAreaTop: 0,
    initData: '',
    userLanguageCode: navigator.language ?? null,
    userId: null,
    conversationId: null,
    ready() {
      // no-op
    },
  }
}
