import type { HostTheme } from '../lib/theme'

export type HostPlatform = 'android' | 'ios' | 'desktop' | 'unknown'

export interface HostContext {
  isTelegram: boolean
  theme: HostTheme
  platform: HostPlatform
  safeAreaTop: number
  initData: string
  userLanguageCode: string | null
  userId: string | null
  conversationId: string | null
  ready(): void
}
