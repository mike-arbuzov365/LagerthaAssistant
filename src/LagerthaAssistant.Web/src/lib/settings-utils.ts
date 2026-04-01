export function formatAiKeySource(source: string): string {
  switch (source) {
    case 'stored':
      return 'Збережений ключ'
    case 'environment':
      return 'Ключ із середовища'
    default:
      return 'Ключ відсутній'
  }
}

export function getScopedUserId(userId: string | undefined): string | null {
  if (!userId || userId === 'anonymous') {
    return null
  }

  return userId
}
