export type SettingsModuleId = 'core' | 'lagertha'

export type SettingsSectionId =
  | 'state'
  | 'locale'
  | 'session'
  | 'ai'
  | 'policy'
  | 'integrations'
  | 'onedriveAuth'
  | 'onedriveOps'

export interface SettingsSectionDefinition {
  id: SettingsSectionId
  module: SettingsModuleId
  title: string
  description?: string
}

export interface BuildSettingsSchemaOptions {
  modules?: SettingsModuleId[]
}

const coreSections: SettingsSectionDefinition[] = [
  {
    id: 'state',
    module: 'core',
    title: 'Поточний стан',
    description: 'Базові дані сесії та поточного policy стану.',
  },
  {
    id: 'locale',
    module: 'core',
    title: 'Мова інтерфейсу',
    description: 'Core налаштування локалізації. За замовчуванням — українська.',
  },
  {
    id: 'session',
    module: 'core',
    title: 'Сесія і сховище',
    description: 'Базові преференси сесії, доступні для всіх ботів платформи.',
  },
]

const lagerthaSections: SettingsSectionDefinition[] = [
  {
    id: 'ai',
    module: 'lagertha',
    title: 'AI налаштування',
    description: 'Провайдер, модель і API ключ для Lagertha.',
  },
  {
    id: 'policy',
    module: 'lagertha',
    title: 'Обмеження Mini App v1',
    description: 'Прозорий показ policy обмежень для поточного релізу.',
  },
  {
    id: 'integrations',
    module: 'lagertha',
    title: 'Інтеграції (Notion)',
    description: 'Операційний стан інтеграцій Lagertha.',
  },
  {
    id: 'onedriveAuth',
    module: 'lagertha',
    title: 'OneDrive авторизація',
    description: 'Device-code flow для входу в OneDrive у межах Mini App.',
  },
  {
    id: 'onedriveOps',
    module: 'lagertha',
    title: 'OneDrive операції',
    description: 'Операції синхронізації та обслуговування індексу.',
  },
]

const moduleRegistry: Record<SettingsModuleId, SettingsSectionDefinition[]> = {
  core: coreSections,
  lagertha: lagerthaSections,
}

export function buildSettingsSchema(options?: BuildSettingsSchemaOptions): SettingsSectionDefinition[] {
  const requestedModules = options?.modules ?? ['core', 'lagertha']
  const uniqueModules = Array.from(new Set(requestedModules))

  return uniqueModules.flatMap((moduleId) => moduleRegistry[moduleId] ?? [])
}
