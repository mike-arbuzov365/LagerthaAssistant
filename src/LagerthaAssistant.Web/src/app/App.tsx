import { useEffect, useMemo } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { getSessionBootstrap } from '../api/client'
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

function SettingsBootSkeleton({ title }: { title: string }) {
  return (
    <div className="settings-page settings-page--skeleton" aria-hidden="true">
      <section className="tg-profile-card settings-hero settings-hero--skeleton">
        <div className="tg-profile-main">
          <div className="tg-avatar skeleton-block skeleton-block--avatar" />
          <div className="settings-skeleton__copy">
            <div className="skeleton-block skeleton-block--title" />
            <div className="skeleton-block skeleton-block--subtitle" />
          </div>
        </div>
        <div className="skeleton-block skeleton-block--chip" />
      </section>

      <section className="tg-card settings-section">
        <div className="settings-section__intro">
          <p className="settings-section__eyebrow">{title}</p>
          <div className="skeleton-block skeleton-block--section" />
          <div className="skeleton-block skeleton-block--body" />
        </div>
        <div className="settings-subsection">
          <div className="skeleton-block skeleton-block--field" />
          <div className="skeleton-block skeleton-block--choice" />
          <div className="skeleton-block skeleton-block--choice" />
        </div>
        <div className="settings-subsection">
          <div className="skeleton-block skeleton-block--field" />
          <div className="skeleton-block skeleton-block--choice" />
        </div>
      </section>

      <div className="settings-savebar settings-savebar--skeleton">
        <div className="skeleton-block skeleton-block--savebar-meta" />
        <div className="skeleton-block skeleton-block--savebar-button" />
      </div>
    </div>
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

        const bootstrap = await getSessionBootstrap({
          channel: host.isTelegram ? 'telegram' : 'api',
          userId: host.userId,
          conversationId: host.conversationId,
          initData: host.initData || undefined,
        })

        const normalizedLocale = resolvePreferredLocale(
          bootstrap.locale.locale ?? bootstrap.policy.defaultLocale,
          host.userLanguageCode,
        )

        setReady({ locale: normalizedLocale, bootstrap, policy: bootstrap.policy })
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
        isSettingsRoute
          ? <SettingsBootSkeleton title={locale === 'en' ? 'General' : 'Загальні'} />
          : (
            <div className="card" role="status" aria-live="polite">
              {locale === 'en' ? 'Loading Mini App…' : 'Завантаження Mini App…'}
            </div>
          )
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
