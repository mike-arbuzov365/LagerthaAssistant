import { createBrowserHost } from './browserHost'
import { createTelegramHost } from './telegramHost'
import type { HostContext } from './types'

const TELEGRAM_HOST_RETRY_DELAYS_MS = [0, 40, 80, 140, 220, 320]

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms)
  })
}

export async function resolveHostContext(): Promise<HostContext> {
  for (const delayMs of TELEGRAM_HOST_RETRY_DELAYS_MS) {
    if (delayMs > 0) {
      await delay(delayMs)
    }

    const telegramHost = createTelegramHost()
    if (telegramHost) {
      return telegramHost
    }
  }

  return createBrowserHost()
}
