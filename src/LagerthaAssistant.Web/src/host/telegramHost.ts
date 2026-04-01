import type { HostContext, HostTheme } from './types'

declare global {
  interface Window {
    Telegram?: {
      WebApp?: {
        initData?: string
        initDataUnsafe?: {
          user?: {
            id?: number
            language_code?: string
          }
        }
        colorScheme?: 'light' | 'dark'
        contentSafeAreaInsets?: { top?: number }
        ready(): void
        expand(): void
      }
    }
  }
}

function resolveTheme(value: string | undefined): HostTheme {
  return value === 'dark' ? 'dark' : 'light'
}

export function createTelegramHost(): HostContext | null {
  const webApp = window.Telegram?.WebApp
  if (!webApp) {
    return null
  }

  const user = webApp.initDataUnsafe?.user

  return {
    isTelegram: true,
    theme: resolveTheme(webApp.colorScheme),
    safeAreaTop: Math.max(0, webApp.contentSafeAreaInsets?.top ?? 0),
    initData: webApp.initData ?? '',
    userLanguageCode: user?.language_code ?? null,
    userId: typeof user?.id === 'number' ? String(user.id) : null,
    ready() {
      webApp.ready()
      webApp.expand()
    },
  }
}
