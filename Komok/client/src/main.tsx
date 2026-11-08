import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import './style.css'

class AppErrorBoundary extends React.Component<React.PropsWithChildren, { hasError: boolean }> {
  state = { hasError: false }

  static getDerivedStateFromError() {
    return { hasError: true }
  }

  render() {
    if (this.state.hasError) {
      return <main className="site-shell"><section className="page-section"><div className="panel form-error" role="alert">
        <h1>Не удалось отобразить страницу</h1>
        <p>Данные обновились некорректно. Обновите страницу и повторите действие.</p>
        <button className="button" onClick={() => window.location.reload()}>Обновить страницу</button>
      </div></section></main>
    }
    return this.props.children
  }
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <AppErrorBoundary><App /></AppErrorBoundary>
  </React.StrictMode>,
)
