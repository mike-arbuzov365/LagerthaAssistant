import { useEffect, useMemo } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { getLocale, getMiniAppPolicy, getSessionBootstrap, verifyMiniAppInitData } from '../api/client'
import { createHostContext } from '../host/createHost'
import { resolvePreferredLocale } from '../lib/locale'
import { DashboardPage } from '../pages/DashboardPage'
import { SettingsPage } from '../pages/SettingsPage'
import { useAppStore } from '../state/appStore'

function AppShellHeader() {
  const locale = useAppStore((s) => s.locale)
  const title = locale === 'en' ? 'Settings' : 'Налаштування'
  const subtitle = locale === 'en' ? 'Lagertha Assistant Bot' : 'Lagertha Assistant Bot'

  return (
    <header className="shell-header">
      <div>
        <h1 className="shell-title">{title}</h1>
        <p className="shell-subtitle">{subtitle}</p>
      </div>
    </header>
  )
}

export function App() {
  const host = useMemo(() => createHostContext(), [])
  const location = useLocation()
  const status = useAppStore((s) => s.status)
  const locale = useAppStore((s) => s.locale)
  const error = useAppStore((s) => s.error)
  const setLoading = useAppStore((s) => s.setLoading)
  const setReady = useAppStore((s) => s.setReady)
  const setError = useAppStore((s) => s.setError)
  const isSettingsRoute = location.pathname === '/settings' || location.pathname === '/miniapp/settings'

  useEffect(() => {
    void (async () => {
      setLoading()

      try {
        host.ready()
        document.documentElement.style.setProperty('--safe-top', `${host.safeAreaTop}px`)
        document.documentElement.dataset.theme = host.theme

        if (host.isTelegram && host.initData) {
          const verification = await verifyMiniAppInitData({ initData: host.initData })
          if (!verification.isValid) {
            throw new Error(`InitData verify failed: ${verification.reason}`)
          }
        }

        const [policy, bootstrap, localeResponse] = await Promise.all([
          getMiniAppPolicy(),
          getSessionBootstrap(host.userId, host.conversationId),
          getLocale(host.userId, host.conversationId),
        ])

        const normalizedLocale = resolvePreferredLocale(
          localeResponse.locale ?? policy.defaultLocale,
          host.userLanguageCode,
        )

        setReady({ locale: normalizedLocale, bootstrap, policy })
      } catch (e) {
        const message = e instanceof Error ? e.message : 'Помилка ініціалізації'
        setError(message)
      }
    })()
  }, [host, setError, setLoading, setReady])

  useEffect(() => {
    document.documentElement.lang = locale
  }, [locale])

  return (
    <div className="shell">
      {!isSettingsRoute && <AppShellHeader />}

      {status === 'loading' && (
        <div className="card" role="status" aria-live="polite">
          {locale === 'en' ? 'Loading Mini App…' : 'Завантаження Mini App…'}
        </div>
      )}
      {status === 'error' && (
        <div className="card status-error" role="alert">
          {locale === 'en' ? 'Error' : 'Помилка'}: {error}
        </div>
      )}
      {status === 'ready' && (
        <main aria-label="Основний контент Mini App">
          <Routes>
            <Route path="/" element={<Navigate to="/settings" replace />} />
            <Route path="/miniapp" element={<Navigate to="/miniapp/settings" replace />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/miniapp/settings" element={<SettingsPage />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/miniapp/dashboard" element={<DashboardPage />} />
            <Route path="*" element={<Navigate to="/settings" replace />} />
          </Routes>
        </main>
      )}
    </div>
  )
}
