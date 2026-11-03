import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'

type TextValue = { txt: string }
type FioValue = { fio: string }
type ApiError = { message?: string }

export default function App() {
  const [textValue, setTextValue] = useState('')
  const [fioValue, setFioValue] = useState('')
  const [textStatus, setTextStatus] = useState('Загружаем значение из PostgreSQL…')
  const [fioStatus, setFioStatus] = useState('Загружаем значение из PostgreSQL…')
  const [savingText, setSavingText] = useState(false)
  const [savingFio, setSavingFio] = useState(false)

  useEffect(() => {
    fetch('/api/text')
      .then(async (response) => {
        if (!response.ok) throw new Error('Не удалось прочитать Text.Txt из базы.')
        return (await response.json()) as TextValue
      })
      .then((data) => {
        setTextValue(data.txt)
        setTextStatus('Загружено из PostgreSQL')
      })
      .catch((error: Error) => setTextStatus(error.message))

    fetch('/api/text2/1')
      .then(async (response) => {
        if (!response.ok) throw new Error('Не удалось прочитать Text2.fio из базы.')
        return (await response.json()) as FioValue
      })
      .then((data) => {
        setFioValue(data.fio)
        setFioStatus('Загружено из PostgreSQL')
      })
      .catch((error: Error) => setFioStatus(error.message))
  }, [])

  async function saveText(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSavingText(true)
    setTextStatus('Сохраняем…')
    try {
      const response = await fetch('/api/text', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ txt: textValue }),
      })
      if (!response.ok) {
        const error = (await response.json()) as ApiError
        throw new Error(error.message ?? 'Не удалось сохранить Text.Txt.')
      }
      const data = (await response.json()) as TextValue
      setTextValue(data.txt)
      setTextStatus('Сохранено в PostgreSQL')
    } catch (error) {
      setTextStatus(error instanceof Error ? error.message : 'Не удалось сохранить Text.Txt.')
    } finally {
      setSavingText(false)
    }
  }

  async function saveFio(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSavingFio(true)
    setFioStatus('Сохраняем…')
    try {
      const response = await fetch('/api/text2/1', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fio: fioValue }),
      })
      if (!response.ok) {
        const error = (await response.json()) as ApiError
        throw new Error(error.message ?? 'Не удалось сохранить Text2.fio.')
      }
      const data = (await response.json()) as FioValue
      setFioValue(data.fio)
      setFioStatus('Сохранено в PostgreSQL')
    } catch (error) {
      setFioStatus(error instanceof Error ? error.message : 'Не удалось сохранить Text2.fio.')
    } finally {
      setSavingFio(false)
    }
  }

  return (
    <main className="card">
      <div className="eyebrow">Komok · приложение</div>
      <h1>Редактирование записей</h1>
      <p className="description">React и TypeScript · ASP.NET Core · Entity Framework Core · PostgreSQL</p>

      <form onSubmit={saveText}>
        <label htmlFor="txt">Значение поля Text.Txt</label>
        <input
          id="txt"
          value={textValue}
          onChange={(event) => setTextValue(event.target.value)}
          maxLength={500}
          required
        />
        <button type="submit" disabled={savingText || textValue.trim().length === 0}>
          {savingText ? 'Сохраняем…' : 'Сохранить Text.Txt'}
        </button>
      </form>
      <p className="status" role="status">{textStatus}</p>

      <hr className="divider" />

      <form onSubmit={saveFio}>
        <label htmlFor="fio">Значение Text2.fio (строка fio1)</label>
        <input
          id="fio"
          value={fioValue}
          onChange={(event) => setFioValue(event.target.value)}
          maxLength={500}
          required
        />
        <button type="submit" disabled={savingFio || fioValue.trim().length === 0}>
          {savingFio ? 'Сохраняем…' : 'Сохранить fio1'}
        </button>
      </form>
      <p className="status" role="status">{fioStatus}</p>
    </main>
  )
}
