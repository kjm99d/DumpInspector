import React, { useState } from 'react'
import { validate } from '../api'

export default function Login({ onLogin, setMessage }) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    if (!username || !password) {
      setMessage('아이디와 비밀번호를 모두 입력하세요.')
      return
    }
    setLoading(true)
    try {
      const res = await validate(username, password)
      if (res.valid) {
        setMessage('')
        onLogin(username)
      } else {
        setMessage('로그인 정보가 올바르지 않습니다.')
      }
    } catch (err) {
      setMessage(err.message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <form className="login-form" onSubmit={handleSubmit}>
      <h2>Welcome back</h2>
      <p className="hint">관리자가 발급한 계정으로 로그인하세요.</p>

      <label>
        Username
        <input
          placeholder="Enter username"
          value={username}
          onChange={e => {
            setUsername(e.target.value)
            setMessage('')
          }}
        />
      </label>

      <label>
        Password
        <input
          placeholder="Enter password"
          type="password"
          value={password}
          onChange={e => {
            setPassword(e.target.value)
            setMessage('')
          }}
        />
      </label>

      <button type="submit" disabled={loading}>
        {loading ? 'Processing…' : 'Login'}
      </button>

      <p className="hint small">계정이 필요하면 관리자에게 요청하세요.</p>
    </form>
  )
}
