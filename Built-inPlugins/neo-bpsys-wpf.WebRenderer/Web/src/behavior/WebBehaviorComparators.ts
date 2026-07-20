export const numberValue = (value: unknown): number | undefined =>
  typeof value === 'number' && Number.isFinite(value)
    ? value
    : typeof value === 'string' && value.trim() !== '' && Number.isFinite(Number(value))
      ? Number(value)
      : undefined

const booleanValue = (value: unknown): boolean | undefined => {
  if (typeof value === 'boolean') return value
  if (typeof value === 'string' && /^(true|false)$/i.test(value.trim())) return value.trim().toLowerCase() === 'true'
  return undefined
}

export const equals = (left: unknown, right: unknown): boolean => {
  const leftNumber = numberValue(left); const rightNumber = numberValue(right)
  if (leftNumber !== undefined && rightNumber !== undefined) return leftNumber === rightNumber
  const leftBoolean = booleanValue(left); const rightBoolean = booleanValue(right)
  if (leftBoolean !== undefined && rightBoolean !== undefined) return leftBoolean === rightBoolean
  if (typeof left === 'string' || typeof right === 'string') return String(left ?? '').toLowerCase() === String(right ?? '').toLowerCase()
  return left === right
}

/** Compare typed behavior values without coercing arrays into comma-separated strings. */
export const compare = (left: unknown, op = 'Equals', right?: unknown): boolean => {
  if (op === 'Exists') return left !== null && left !== undefined
  if (op === 'Contains' || op === 'NotContains') {
    const contains = Array.isArray(left)
      ? left.some(item => equals(item, right))
      : typeof left === 'string'
        ? left.toLowerCase().includes(String(right ?? '').toLowerCase())
        : false
    return op === 'Contains' ? contains : !contains
  }
  if (op === 'Equals') return equals(left, right)
  if (op === 'NotEquals') return !equals(left, right)
  const leftNumber = numberValue(left); const rightNumber = numberValue(right)
  const order = leftNumber !== undefined && rightNumber !== undefined
    ? leftNumber - rightNumber
    : String(left ?? '').localeCompare(String(right ?? ''), undefined, { sensitivity: 'accent' })
  switch (op) {
    case 'GreaterThan': return order > 0
    case 'GreaterThanOrEqual': return order >= 0
    case 'LessThan': return order < 0
    case 'LessThanOrEqual': return order <= 0
    default: return false
  }
}
