import TelegramWebApp from '@twa-dev/sdk'

export type TelegramBridgeLike = {
  initData?: string
  platform?: string
  colorScheme?: 'light' | 'dark'
  isExpanded?: boolean
  isFullscreen?: boolean
  safeAreaInset?: {
    top?: number
    bottom?: number
    left?: number
    right?: number
  }
  contentSafeAreaInset?: {
    top?: number
    bottom?: number
    left?: number
    right?: number
  }
  contentSafeAreaInsets?: { top?: number }
  safeAreaInsets?: { top?: number }
  ready?: () => void
  close?: (options?: { return_back?: boolean }) => void
  enableClosingConfirmation?: () => void
  disableClosingConfirmation?: () => void
  isClosingConfirmationEnabled?: boolean
  BackButton?: {
    hide?: () => void
  }
  MainButton?: {
    hide?: () => void
  }
  SecondaryButton?: {
    hide?: () => void
  }
  SettingsButton?: {
    hide?: () => void
  }
}

function hasTelegramContext(bridge: TelegramBridgeLike | undefined): boolean {
  return typeof bridge?.initData === 'string' && bridge.initData.trim().length > 0
}

export function resolveTelegramBridge(): TelegramBridgeLike | undefined {
  const globalBridge = window.Telegram?.WebApp as TelegramBridgeLike | undefined
  if (hasTelegramContext(globalBridge)) {
    return globalBridge
  }

  const sdkBridge = TelegramWebApp as TelegramBridgeLike | undefined
  if (hasTelegramContext(sdkBridge)) {
    return sdkBridge
  }

  return undefined
}
