import React, { useEffect, useState } from 'react'
import Login from './components/Login'
import Upload from './components/Upload'
import Dumps from './components/Dumps'
import ChangePassword from './components/ChangePassword'
import AdminPanel from './components/AdminPanel'
import Home from './components/Home'
import { isAdmin } from './api'

export default function App() {
  const [view, setView] = useState('home')
  const [user, setUser] = useState(sessionStorage.getItem('di_username') || null)
  const [isAdminUser, setIsAdminUser] = useState(false)
  const [message, setMessage] = useState('')

  function onLogin(username) {
    setUser(username)
    sessionStorage.setItem('di_username', username)
    setView('home')
    setMessage('')
  }

  useEffect(() => {
    let cancelled = false
    async function checkAdmin() {
      if (!user) {
        setIsAdminUser(false)
        return
      }
      try {
        const res = await isAdmin(user)
        if (!cancelled) setIsAdminUser(Boolean(res.isAdmin))
      } catch {
        if (!cancelled) setIsAdminUser(false)
      }
    }
    checkAdmin()
    return () => { cancelled = true }
  }, [user])

  useEffect(() => {
    if (!user) setView('home')
  }, [user])

  function logout() {
    sessionStorage.removeItem('di_username')
    setUser(null)
    setIsAdminUser(false)
    setView('home')
    setMessage('')
  }

  if (!user) {
    return (
      <div className="app landing">
        <div className="landing-hero">
          <div className="logo large" aria-hidden>DI</div>
          <h1>DumpInspector</h1>
          <p>Sign in to access crash dump uploads, analysis results, and administrative tools.</p>
        </div>
        <div className="login-card">
          <Login onLogin={onLogin} setMessage={setMessage} />
          {message && <div className="toast inline">{message}</div>}
        </div>
      </div>
    )
  }

  const navItems = [
    { key: 'home', label: 'Overview', visible: true, disabled: false },
    { key: 'upload', label: 'Upload', visible: Boolean(user), disabled: false },
    { key: 'dumps', label: 'Dumps', visible: true, disabled: false },
    { key: 'change', label: 'Change Password', visible: Boolean(user), disabled: false },
    { key: 'admin', label: 'Admin Panel', visible: isAdminUser, disabled: false }
  ]

  return (
    <div className="app workspace">
      <header className="topbar">
        <div className="brand">
          <span className="logo" aria-hidden>DI</span>
          <span>DumpInspector</span>
        </div>
        <div className="user-area small">
          {user ? (
            <>
              <span className="user-chip">{user}{isAdminUser ? ' • Admin' : ''}</span>
              <button className="ghost" onClick={logout}>Logout</button>
            </>
          ) : (
            <span className="muted">Not signed in</span>
          )}
        </div>
      </header>

      <nav className="tabbar">
        {navItems.filter(item => item.visible).map(item => (
          <button
            key={item.key}
            className={view === item.key ? 'active' : ''}
            onClick={() => {
              setView(item.key)
              setMessage('')
            }}
            disabled={item.disabled}
          >
            {item.label}
          </button>
        ))}
      </nav>

      <main className="content">
        {message && <div className="toast in-content">{message}</div>}
        {view === 'home' && <Home isAdmin={isAdminUser} />}
        {view === 'upload' && user && <Upload username={user} setMessage={setMessage} />}
        {view === 'dumps' && <Dumps />}
        {view === 'change' && user && <ChangePassword username={user} setMessage={setMessage} />}
        {isAdminUser && view === 'admin' && <AdminPanel setMessage={setMessage} />}
      </main>
    </div>
  )
}
