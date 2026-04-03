import { useEffect, useState } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { getSessionBootstrap } from '../api/client'
import { resolveHostContext } from '../host/createHost'
import { emitMiniAppDiagnostic } from '../lib/miniAppDiagnostics'
import { resolveTelegramBridge } from '../lib/telegramBridge'
import { resolvePreferredLocale } from '../lib/locale'
import { resolveAppliedTheme, type HostTheme } from '../lib/theme'
import { DashboardPage } from '../pages/DashboardPage'
import { SettingsPage } from '../pages/SettingsPage'
import { useAppStore } from '../state/appStore'

function AppShellHeader() {
  const locale = useAppStore((s) => s.locale)
  const title = locale === 'en' ? 'Settings' : 'Налаштування'

  return (
    <header className="shell-header">
      <div>
        <h1 className="shell-title">{title}</h1>
        <p className="shell-subtitle">Lagertha Assistant Bot</p>
      </div>
    </header>
  )
}

function SettingsBootSkeleton() {
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
          <div className="skeleton-block skeleton-block--eyebrow" />
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
  const location = useLocation()
  const status = useAppStore((s) => s.status)
  const locale = useAppStore((s) => s.locale)
  const themeMode = useAppStore((s) => s.themeMode)
  const error = useAppStore((s) => s.error)
  const setLoading = useAppStore((s) => s.setLoading)
  const setReady = useAppStore((s) => s.setReady)
  const setError = useAppStore((s) => s.setError)
  const [hostTheme, setHostTheme] = useState<HostTheme>('light')
  const isSettingsRoute = location.pathname === '/settings' || location.pathname === '/miniapp/settings'

  useEffect(() => {
    let cancelled = false

    void (async () => {
      setLoading()

      try {
        const host = await resolveHostContext()
        if (cancelled) {
          return
        }

        emitMiniAppDiagnostic({
          eventType: 'host.resolved',
          severity: host.isTelegram ? 'info' : 'warn',
          message: host.isTelegram ? 'Resolved Telegram host context.' : 'Fell back to browser host context.',
          isTelegram: host.isTelegram,
          hostSource: host.source,
          platform: host.platform,
          userId: host.userId,
          conversationId: host.conversationId,
          hasInitData: host.initData.length > 0,
          hasWebApp: Boolean(resolveTelegramBridge()),
          details: {
            safeAreaTop: host.safeAreaTop,
          },
        })

        setHostTheme(host.theme)
        host.ready()
        document.documentElement.style.setProperty('--safe-top', `${host.safeAreaTop}px`)

        emitMiniAppDiagnostic({
          eventType: 'bootstrap.request',
          severity: 'info',
          message: 'Requesting Mini App bootstrap payload.',
          isTelegram: host.isTelegram,
          hostSource: host.source,
          platform: host.platform,
          channel: host.isTelegram ? 'telegram' : 'api',
          userId: host.userId,
          conversationId: host.conversationId,
          hasInitData: host.initData.length > 0,
          hasWebApp: Boolean(resolveTelegramBridge()),
        })

        const bootstrap = await getSessionBootstrap({
          channel: host.isTelegram ? 'telegram' : 'api',
          userId: host.userId,
          conversationId: host.conversationId,
          initData: host.initData || undefined,
        })

        if (cancelled) {
          return
        }

        const normalizedLocale = resolvePreferredLocale(
          bootstrap.locale.locale ?? bootstrap.policy.defaultLocale,
          host.userLanguageCode,
        )

        emitMiniAppDiagnostic({
          eventType: 'bootstrap.success',
          severity: 'info',
          message: 'Mini App bootstrap loaded.',
          isTelegram: host.isTelegram,
          hostSource: host.source,
          platform: host.platform,
          channel: bootstrap.scope.channel,
          userId: bootstrap.scope.userId,
          conversationId: bootstrap.scope.conversationId,
          hasInitData: host.initData.length > 0,
          hasWebApp: Boolean(resolveTelegramBridge()),
          locale: normalizedLocale,
          details: {
            bootstrapLocale: bootstrap.locale.locale,
            defaultLocale: bootstrap.policy.defaultLocale,
          },
        })

        setReady({
          locale: normalizedLocale,
          bootstrap,
          policy: bootstrap.policy,
          host: {
            isTelegram: host.isTelegram,
            source: host.source,
            platform: host.platform,
            theme: host.theme,
            initData: host.initData,
            userId: host.userId,
            conversationId: host.conversationId,
          },
        })
      } catch (e) {
        if (cancelled) {
          return
        }

        const message = e instanceof Error ? e.message : 'Помилка ініціалізації'
        emitMiniAppDiagnostic({
          eventType: 'bootstrap.failure',
          severity: 'error',
          message,
          isTelegram: Boolean(resolveTelegramBridge()),
          hostSource: resolveTelegramBridge() ? 'telegram-webapp' : 'browser',
          platform: resolveTelegramBridge()?.platform ?? 'unknown',
          hasInitData: Boolean(resolveTelegramBridge()?.initData),
          hasWebApp: Boolean(resolveTelegramBridge()),
        })
        setError(message)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [setError, setLoading, setReady])

  useEffect(() => {
    document.documentElement.lang = locale
  }, [locale])

  useEffect(() => {
    document.documentElement.dataset.theme = resolveAppliedTheme(themeMode, hostTheme)
  }, [hostTheme, themeMode])

  return (
    <div className="shell">
      {!isSettingsRoute && <AppShellHeader />}

      {status === 'loading' && (
        isSettingsRoute
          ? <SettingsBootSkeleton />
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
