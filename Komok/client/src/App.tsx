import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'

type TextValue = { txt: string }

export default function App() {
  const [value, setValue] = useState('')
  const [status, setStatus] = useState('Загружаем значение из PostgreSQL…')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    fetch('/api/text')
      .then(async (response) => {
        if (!response.ok) throw new Error('Не удалось прочитать значение из базы.')
        return (await response.json()) as TextValue
      })
      .then((data) => {
        setValue(data.txt)
        setStatus('Значение загружено из PostgreSQL')
      })
      .catch((error: Error) => setStatus(error.message))
  }, [])

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    setStatus('Сохраняем…')
    try {
      const response = await fetch('/api/text', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ txt: value }),
      })
      if (!response.ok) {
        const error = (await response.json()) as { message?: string }
        throw new Error(error.message ?? 'Не удалось сохранить значение.')
      }
      const data = (await response.json()) as TextValue
      setValue(data.txt)
      setStatus('Сохранено в PostgreSQL')
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Не удалось сохранить значение.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <main className="card">
      <div className="eyebrow">Komok · приложение</div>
      <h1>Редактирование записи</h1>
      <p className="description">React и TypeScript · ASP.NET Core · Entity Framework Core · PostgreSQL</p>
      <form onSubmit={save}>
        <label htmlFor="txt">Значение поля Text.Txt</label>
        <input
          id="txt"
          value={value}
          onChange={(event) => setValue(event.target.value)}
          maxLength={500}
          required
        />
        <button type="submit" disabled={saving || value.trim().length === 0}>
          {saving ? 'Сохраняем…' : 'Сохранить'}
        </button>
      </form>
      <p className="status" role="status">{status}</p>
    </main>
  )
}