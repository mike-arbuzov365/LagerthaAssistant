import { createBrowserHost } from './browserHost'
import { createTelegramHost } from './telegramHost'
import type { HostContext } from './types'

export function createHostContext(): HostContext {
  return createTelegramHost() ?? createBrowserHost()
}
