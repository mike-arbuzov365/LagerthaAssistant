import type { HostTheme } from '../lib/theme'

export type HostPlatform = 'android' | 'ios' | 'desktop' | 'unknown'
export type HostSource = 'telegram-webapp' | 'telegram-launch-params' | 'browser'

export interface HostContext {
  isTelegram: boolean
  source: HostSource
  theme: HostTheme
  platform: HostPlatform
  safeAreaTop: number
  initData: string
  userLanguageCode: string | null
  userId: string | null
  conversationId: string | null
  ready(): void
}
