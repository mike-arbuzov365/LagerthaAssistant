import type { HostTheme } from '../lib/theme'
import { resolveTelegramBridge, type TelegramBridgeLike } from '../lib/telegramBridge'
import type { HostContext, HostPlatform } from './types'

declare global {
  interface Window {
    Telegram?: {
      WebView?: {
        postEvent?: (eventType: string, callback?: boolean, eventData?: Record<string, unknown>) => void
      }
      WebApp?: {
        initData?: string
        initDataUnsafe?: {
          user?: {
            id?: number
            language_code?: string
          }
        }
        colorScheme?: 'light' | 'dark'
        platform?: string
        contentSafeAreaInsets?: { top?: number }
        isExpanded?: boolean
        isFullscreen?: boolean
        ready(): void
        expand(): void
        requestFullscreen?: () => void
        onEvent?: (eventType: string, handler: (payload?: unknown) => void) => void
        offEvent?: (eventType: string, handler: (payload?: unknown) => void) => void
      }
    }
  }
}

interface TelegramLaunchParams {
  initData: string
  platform: HostPlatform
  theme: HostTheme
  userId: string | null
  userLanguageCode: string | null
}

type TelegramWebApp = TelegramBridgeLike & NonNullable<NonNullable<typeof window.Telegram>['WebApp']>

function resolveTheme(value: string | undefined): HostTheme {
  return value === 'dark' ? 'dark' : 'light'
}

function resolvePlatform(value: string | undefined): HostPlatform {
  const normalized = value?.trim().toLowerCase()

  return normalized === 'android' || normalized === 'ios' || normalized === 'tdesktop'
    ? (normalized === 'tdesktop' ? 'desktop' : normalized)
    : 'unknown'
}

function parseInitDataUser(
  initData: string | undefined,
): {
  id: string | null
  languageCode: string | null
} {
  if (!initData) {
    return { id: null, languageCode: null }
  }

  try {
    const params = new URLSearchParams(initData)
    const userPayload = params.get('user')
    if (!userPayload) {
      return { id: null, languageCode: null }
    }

    const parsed = JSON.parse(userPayload) as {
      id?: number | string
      language_code?: string
    }

    const id = typeof parsed.id === 'number'
      ? String(parsed.id)
      : (typeof parsed.id === 'string' && parsed.id.trim().length > 0 ? parsed.id.trim() : null)

    const languageCode = typeof parsed.language_code === 'string' && parsed.language_code.trim().length > 0
      ? parsed.language_code.trim()
      : null

    return { id, languageCode }
  } catch {
    return { id: null, languageCode: null }
  }
}

function collectTelegramLaunchSearchParams(): URLSearchParams[] {
  const groups: URLSearchParams[] = []

  const pushSegment = (raw: string | undefined) => {
    const trimmed = raw?.trim()
    if (!trimmed) {
      return
    }

    const normalized = trimmed.startsWith('?') || trimmed.startsWith('#')
      ? trimmed.slice(1)
      : trimmed

    if (normalized.length === 0) {
      return
    }

    groups.push(new URLSearchParams(normalized))
  }

  pushSegment(window.location.search)
  pushSegment(window.location.hash)

  const hash = window.location.hash.startsWith('#')
    ? window.location.hash.slice(1)
    : window.location.hash
  const hashQueryIndex = hash.indexOf('?')
  if (hashQueryIndex >= 0) {
    pushSegment(hash.slice(hashQueryIndex + 1))
  }

  return groups
}

function tryParseThemeFromLaunchParams(raw: string | null): HostTheme | null {
  if (!raw) {
    return null
  }

  try {
    const parsed = JSON.parse(raw) as { bg_color?: string; secondary_bg_color?: string }
    const color = parsed.bg_color ?? parsed.secondary_bg_color
    if (!color) {
      return null
    }

    const normalized = color.trim().toLowerCase()
    if (!/^#?[0-9a-f]{6}$/i.test(normalized)) {
      return null
    }

    const hex = normalized.startsWith('#') ? normalized.slice(1) : normalized
    const red = Number.parseInt(hex.slice(0, 2), 16)
    const green = Number.parseInt(hex.slice(2, 4), 16)
    const blue = Number.parseInt(hex.slice(4, 6), 16)
    const luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255
    return luminance < 0.45 ? 'dark' : 'light'
  } catch {
    return null
  }
}

function readTelegramLaunchParams(): TelegramLaunchParams | null {
  const parameterGroups = collectTelegramLaunchSearchParams()
  let initData = ''
  let platform = 'unknown'
  let theme: HostTheme | null = null

  for (const parameters of parameterGroups) {
    const candidateInitData = parameters.get('tgWebAppData')?.trim()
    if (!initData && candidateInitData) {
      initData = candidateInitData
    }

    const candidatePlatform = parameters.get('tgWebAppPlatform')
    if (platform === 'unknown' && candidatePlatform) {
      platform = candidatePlatform
    }

    if (!theme) {
      theme = tryParseThemeFromLaunchParams(parameters.get('tgWebAppThemeParams'))
    }
  }

  if (initData.length === 0) {
    return null
  }

  const parsedUser = parseInitDataUser(initData)
  return {
    initData,
    platform: resolvePlatform(platform),
    theme: theme ?? 'light',
    userId: parsedUser.id,
    userLanguageCode: parsedUser.languageCode,
  }
}

function requestPreferredViewport(webApp: TelegramWebApp, platform: HostPlatform) {
  if (platform === 'android' || platform === 'ios') {
    webApp.expand()

    try {
      webApp.requestFullscreen?.()
    } catch {
      // Best-effort only.
    }
  }
}

function scheduleTelegramReady(platform: HostPlatform) {
  const retryDelaysMs = [0, 50, 120, 240, 420]

  for (const delayMs of retryDelaysMs) {
    window.setTimeout(() => {
      const webApp = resolveTelegramBridge() as TelegramWebApp | undefined
      if (!webApp) {
        return
      }

      try {
        webApp.ready()
        requestPreferredViewport(webApp, platform)
      } catch {
        // Best-effort only.
      }
    }, delayMs)
  }
}

export function createTelegramHost(): HostContext | null {
  const webApp = resolveTelegramBridge() as TelegramWebApp | undefined
  const launchParams = readTelegramLaunchParams()

  if (!webApp && !launchParams) {
    return null
  }

  const initData = webApp?.initData?.trim() || launchParams?.initData || ''
  const unsafeUser = webApp?.initDataUnsafe?.user
  const parsedInitDataUser = parseInitDataUser(initData)
  const userId = typeof unsafeUser?.id === 'number'
    ? String(unsafeUser.id)
    : (launchParams?.userId ?? parsedInitDataUser.id)
  const userLanguageCode = unsafeUser?.language_code
    ?? launchParams?.userLanguageCode
    ?? parsedInitDataUser.languageCode

  if (!userId || initData.length === 0) {
    return null
  }

  const platform = resolvePlatform(webApp?.platform) !== 'unknown'
    ? resolvePlatform(webApp?.platform)
    : (launchParams?.platform ?? 'unknown')

  const theme = webApp?.colorScheme
    ? resolveTheme(webApp.colorScheme)
    : (launchParams?.theme ?? 'light')

  return {
    isTelegram: true,
    source: webApp?.initData?.trim().length ? 'telegram-webapp' : 'telegram-launch-params',
    theme,
    platform,
    safeAreaTop: Math.max(0, webApp?.contentSafeAreaInsets?.top ?? 0),
    initData,
    userLanguageCode,
    userId,
    conversationId: userId,
    ready() {
      scheduleTelegramReady(platform)
    },
  }
}
