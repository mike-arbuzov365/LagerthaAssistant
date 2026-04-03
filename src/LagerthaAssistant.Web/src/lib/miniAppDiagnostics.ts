import { postMiniAppDiagnostic } from '../api/client'
import type { MiniAppDiagnosticRequest } from '../api/contracts'

const sessionId = globalThis.crypto?.randomUUID?.() ?? `miniapp-${Date.now()}`

export function emitMiniAppDiagnostic(
  request: Omit<MiniAppDiagnosticRequest, 'sessionId' | 'path'>,
): void {
  void postMiniAppDiagnostic({
    sessionId,
    path: window.location.pathname,
    ...request,
  }).catch(() => {
    // Diagnostics must never break the user flow.
  })
}
