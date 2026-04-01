import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  getAiKeyStatus,
  getAiModel,
  getAiProvider,
  getGraphStatus,
  getIntegrationNotionStatus,
} from '../api/client'
import { buildReadinessSignals, readinessClassName } from '../lib/dashboard-utils'
import { getScopedUserId } from '../lib/settings-utils'
import { useAppStore } from '../state/appStore'

export function DashboardPage() {
  const locale = useAppStore((s) => s.locale)
  const bootstrap = useAppStore((s) => s.bootstrap)
  const policy = useAppStore((s) => s.policy)

  const scopedUserId = useMemo(
    () => getScopedUserId(bootstrap?.scope.userId),
    [bootstrap?.scope.userId],
  )

  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [graphStatus, setGraphStatus] = useState(bootstrap?.graph ?? null)
  const [integrationStatus, setIntegrationStatus] = useState<Awaited<ReturnType<typeof getIntegrationNotionStatus>> | null>(null)
  const [aiStatus, setAiStatus] = useState<{
    provider: string
    model: string
    hasStoredKey: boolean
    source: string
  }>({
    provider: 'openai',
    model: '—',
    hasStoredKey: false,
    source: 'missing',
  })

  const loadDashboard = useCallback(async () => {
    if (!bootstrap) {
      return
    }

    setLoading(true)
    setLoadError(null)

    try {
      const provider = await getAiProvider(scopedUserId)
      const [model, keyStatus, graph, notion] = await Promise.all([
        getAiModel(scopedUserId, provider.provider),
        getAiKeyStatus(scopedUserId, provider.provider),
        getGraphStatus(),
        getIntegrationNotionStatus(),
      ])

      setAiStatus({
        provider: provider.provider,
        model: model.model,
        hasStoredKey: keyStatus.hasStoredKey,
        source: keyStatus.apiKeySource,
      })
      setGraphStatus(graph)
      setIntegrationStatus(notion)
    } catch (error) {
      setLoadError(error instanceof Error ? error.message : 'Не вдалося завантажити статуси Dashboard')
    } finally {
      setLoading(false)
    }
  }, [bootstrap, scopedUserId])

  useEffect(() => {
    if (!bootstrap) {
      return
    }

    setGraphStatus(bootstrap.graph)
    void loadDashboard()
  }, [bootstrap, loadDashboard])

  if (!bootstrap || !policy) {
    return <div className="card">Немає даних bootstrap.</div>
  }

  const activeGraphStatus = graphStatus ?? bootstrap.graph
  const readinessSignals = buildReadinessSignals({
    locale,
    defaultLocale: policy.defaultLocale,
    oneDriveConfigured: activeGraphStatus.isConfigured,
    oneDriveAuthenticated: activeGraphStatus.isAuthenticated,
    aiHasStoredKey: aiStatus.hasStoredKey,
    notionVocabularyEnabled: integrationStatus?.notionVocabulary.enabled ?? false,
    notionFoodEnabled: integrationStatus?.notionFood.enabled ?? false,
  })

  return (
    <div className="grid" aria-busy={loading}>
      <div className="card">
        <h3>Dashboard Wave 1</h3>
        <div className="kv">
          <span>Канал</span>
          <span>{bootstrap.scope.channel}</span>
        </div>
        <div className="kv">
          <span>Ідентифікатор користувача</span>
          <span>{bootstrap.scope.userId}</span>
        </div>
        <div className="kv">
          <span>Ідентифікатор діалогу</span>
          <span>{bootstrap.scope.conversationId}</span>
        </div>
        <div className="actions-row" style={{ marginTop: '12px' }}>
          <button type="button" className="btn-secondary" onClick={() => void loadDashboard()} disabled={loading}>
            Оновити статуси
          </button>
        </div>
        {loading && <p className="muted-text" role="status" aria-live="polite" style={{ marginTop: '12px' }}>Оновлюємо live-статуси…</p>}
        {loadError && <p className="status-error" role="alert" style={{ marginTop: '12px' }}>{loadError}</p>}
      </div>

      <div className="card">
        <h3>Readiness</h3>
        {readinessSignals.map((signal) => (
          <div className="kv" key={signal.id}>
            <span>{signal.label}</span>
            <span className={readinessClassName(signal.state)}>{signal.details}</span>
          </div>
        ))}
      </div>

      <div className="card">
        <h3>OneDrive (live)</h3>
        <div className="kv">
          <span>Налаштування</span>
          <span>{activeGraphStatus.isConfigured ? 'Готово' : 'Вимкнено'}</span>
        </div>
        <div className="kv">
          <span>Статус</span>
          <span className={activeGraphStatus.isAuthenticated ? 'status-ok' : 'status-error'}>
            {activeGraphStatus.isAuthenticated ? 'Підключено' : 'Відключено'}
          </span>
        </div>
        <div className="kv">
          <span>Токен до</span>
          <span>{activeGraphStatus.accessTokenExpiresAtUtc ?? 'Немає'}</span>
        </div>
        <div className="kv">
          <span>Повідомлення</span>
          <span>{activeGraphStatus.message || '—'}</span>
        </div>
      </div>

      <div className="card">
        <h3>AI (live)</h3>
        <div className="kv">
          <span>Провайдер</span>
          <span>{aiStatus.provider}</span>
        </div>
        <div className="kv">
          <span>Модель</span>
          <span>{aiStatus.model}</span>
        </div>
        <div className="kv">
          <span>Ключ</span>
          <span>{aiStatus.hasStoredKey ? 'Збережено' : 'Відсутній'}</span>
        </div>
        <div className="kv">
          <span>Джерело ключа</span>
          <span>{aiStatus.source}</span>
        </div>
      </div>

      <div className="card">
        <h3>Інтеграції (live)</h3>
        {!integrationStatus && <p className="muted-text" role="status" aria-live="polite">Очікуємо статус інтеграцій…</p>}
        {integrationStatus && (
          <>
            <div className="kv">
              <span>Notion Vocabulary</span>
              <span>{integrationStatus.notionVocabulary.enabled ? 'Увімкнено' : 'Вимкнено'}</span>
            </div>
            <div className="kv">
              <span>Черга / помилки</span>
              <span>{integrationStatus.notionVocabulary.pendingCards} / {integrationStatus.notionVocabulary.failedCards}</span>
            </div>
            <div className="kv">
              <span>Notion Food</span>
              <span>{integrationStatus.notionFood.enabled ? 'Увімкнено' : 'Вимкнено'}</span>
            </div>
            <div className="kv">
              <span>Inventory+Grocery pending/failed</span>
              <span>
                {integrationStatus.notionFood.inventoryPendingOrFailed + integrationStatus.notionFood.groceryPendingOrFailed}
              </span>
            </div>
          </>
        )}
      </div>

      <div className="card">
        <h3>Політика Mini App v1</h3>
        <div className="kv">
          <span>Мова за замовчуванням</span>
          <span>{policy.defaultLocale}</span>
        </div>
        <div className="kv">
          <span>Поточна мова UI</span>
          <span>{locale}</span>
        </div>
        <div className="kv">
          <span>Політика сховища</span>
          <span>{policy.storageModePolicy}</span>
        </div>
        <div className="kv">
          <span>Дозволені режими сховища</span>
          <span>{policy.allowedStorageModes.join(', ')}</span>
        </div>
      </div>
    </div>
  )
}
