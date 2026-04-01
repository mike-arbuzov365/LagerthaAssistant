export type HostTheme = 'light' | 'dark'

export interface HostContext {
  isTelegram: boolean
  theme: HostTheme
  safeAreaTop: number
  initData: string
  userLanguageCode: string | null
  userId: string | null
  ready(): void
}
