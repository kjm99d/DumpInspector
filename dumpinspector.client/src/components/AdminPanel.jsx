import React, { useEffect, useState } from 'react'
import {
  adminCreateUser,
  adminListUsers,
  adminForceReset,
  adminDeleteUser,
  adminGetLogs,
  adminUploadPdb,
  getOptions,
  saveOptions
} from '../api'
import OptionsEditor from './OptionsEditor'

export default function AdminPanel({ setMessage }) {
  const [users, setUsers] = useState([])
  const [usersLoading, setUsersLoading] = useState(false)
  const [username, setUsername] = useState('')
  const [email, setEmail] = useState('')
  const [tempPassword, setTempPassword] = useState(null)
  const [logs, setLogs] = useState([])
  const [logsLoading, setLogsLoading] = useState(false)
  const [selectedLog, setSelectedLog] = useState(null)
  const [pdbFile, setPdbFile] = useState(null)
  const [pdbProduct, setPdbProduct] = useState('')
  const [pdbVersion, setPdbVersion] = useState('')
  const [pdbComment, setPdbComment] = useState('')
  const [pdbUploading, setPdbUploading] = useState(false)
  const [pdbResult, setPdbResult] = useState(null)
  const [pdbInputKey, setPdbInputKey] = useState(() => Date.now())

  useEffect(() => {
    refreshUsers()
    refreshLogs()
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function refreshUsers() {
    setUsersLoading(true)
    try {
      const data = await adminListUsers()
      const normalized = Array.isArray(data) ? data : []
      setUsers(normalized)
    } catch (e) {
      setMessage(e.message)
      setUsers([])
    } finally {
      setUsersLoading(false)
    }
  }

  async function doCreate() {
    if (!username) {
      setMessage('아이디를 입력하세요.')
      return
    }
    if (!email || !email.includes('@')) {
      setMessage('유효한 이메일을 입력하세요.')
      return
    }
    try {
      await adminCreateUser(username, email)
      setMessage('사용자 생성 완료 (임시 비밀번호를 이메일로 전송했습니다)')
      setUsername('')
      setEmail('')
      await refreshUsers()
    } catch (e) {
      setMessage(e.message)
    }
  }

  async function doForceReset(userName) {
    if (!userName) {
      setMessage('임시 비밀번호를 발급하려면 사용자를 선택하세요.')
      return
    }
    try {
      const res = await adminForceReset(userName)
      setTempPassword({ username: userName, password: res.temporaryPassword })
      setMessage(`임시 비밀번호가 생성되었습니다: ${res.temporaryPassword}`)
      await refreshUsers()
    } catch (e) {
      setMessage(e.message)
    }
  }

  async function refreshLogs() {
    setLogsLoading(true)
    try {
      const data = await adminGetLogs()
      const normalized = Array.isArray(data) ? data : []
      setLogs(normalized)
      setSelectedLog(null)
    } catch (e) {
      setMessage(e.message)
      setLogs([])
    } finally {
      setLogsLoading(false)
    }
  }

  async function doDelete(userName) {
    if (!userName) return
    if (!window.confirm(`정말로 사용자 ${userName}를 삭제할까요?`)) return
    try {
      await adminDeleteUser(userName)
      setMessage(`사용자 ${userName} 삭제 완료`)
      setTempPassword(null)
      await refreshUsers()
    } catch (e) {
      setMessage(e.message)
    }
  }

  async function handlePdbUpload() {
    if (!pdbFile) {
      setMessage('업로드할 PDB 파일을 선택하세요.')
      return
    }
    setPdbUploading(true)
    try {
      const res = await adminUploadPdb(pdbFile, {
        productName: pdbProduct.trim(),
        version: pdbVersion.trim(),
        comment: pdbComment.trim()
      })
      setPdbResult(res)
      setMessage(res.message ?? 'PDB가 심볼 스토어에 추가되었습니다.')
      setPdbFile(null)
      setPdbInputKey(Date.now())
    } catch (e) {
      setMessage(e.message)
    } finally {
      setPdbUploading(false)
    }
  }

  return (
    <section className="admin-panel">
      <h2>관리 기능</h2>

      <div className="admin-grid">
        <div className="admin-card">
          <h3>사용자 생성</h3>
          <input placeholder="아이디" value={username} onChange={e => setUsername(e.target.value)} />
          <input placeholder="이메일" type="email" value={email} onChange={e => setEmail(e.target.value)} />
          <div className="row">
            <button onClick={doCreate}>생성</button>
          </div>
        </div>

        <div className="admin-card">
          <h3>플랫폼 옵션</h3>
          <OptionsEditor
            embedded
            loadOptions={getOptions}
            saveOptions={saveOptions}
            setMessage={setMessage}
          />
        </div>
        <div className="admin-card">
          <h3>PDB 업로드</h3>
          <input
            key={pdbInputKey}
            type="file"
            accept=".pdb"
            onChange={e => {
              const file = e.target.files?.[0]
              setPdbFile(file || null)
            }}
          />
          <label>Product Name (미입력 시 옵션값 사용)</label>
          <input value={pdbProduct} onChange={e => setPdbProduct(e.target.value)} placeholder="예: DumpInspector" />
          <label>Version (선택)</label>
          <input value={pdbVersion} onChange={e => setPdbVersion(e.target.value)} placeholder="예: 1.0.0.0" />
          <label>Comment (선택)</label>
          <input value={pdbComment} onChange={e => setPdbComment(e.target.value)} placeholder="예: March hotfix" />
          <div className="row">
            <button onClick={handlePdbUpload} disabled={pdbUploading}>
              {pdbUploading ? '업로드 중...' : 'PDB 등록'}
            </button>
          </div>
          {pdbResult && (
            <div className="pdb-result">
              <p>
                <strong>Symbol Store</strong> <code>{pdbResult.symbolStoreRoot}</code>
              </p>
              <p>
                <strong>Product</strong> {pdbResult.product ?? '—'}{pdbResult.version && <> / v{pdbResult.version}</>}
              </p>
              <p>
                <strong>Original File</strong> {pdbResult.originalFileName}
              </p>
              <p>
                <strong>Command</strong> <code>{pdbResult.symStoreCommand}</code>
              </p>
              <label>symstore 출력</label>
              <pre>{pdbResult.symStoreOutput || '출력이 없습니다.'}</pre>
            </div>
          )}
        </div>
      </div>

      <div className="admin-card full">
        <div className="admin-list-header">
          <h3>사용자 목록</h3>
          <button onClick={refreshUsers} disabled={usersLoading}>
            {usersLoading ? '로딩 중...' : '새로고침'}
          </button>
        </div>
        {tempPassword && (
          <div className="temp-password">
            <strong>{tempPassword.username}</strong> 임시 비밀번호:
            <code>{tempPassword.password}</code>
          </div>
        )}
        <ul className="admin-user-list">
          {users.length === 0 && (
            <li className="empty">
              {usersLoading ? '사용자 목록을 불러오는 중입니다...' : '등록된 사용자가 아직 없습니다.'}
            </li>
          )}
          {users.map((user, idx) => {
            const name = user.username ?? user.Username
            const admin = user.isAdmin ?? user.IsAdmin
            const email = user.email ?? user.Email ?? '—'
            return (
              <li key={name ?? idx}>
                <div className="user-meta">
                  <strong>{name || '(unknown)'}</strong>
                  {admin && <span className="pill">관리자</span>}
                </div>
                <div className="user-email">{email}</div>
                <div className="user-actions">
                  <button type="button" onClick={() => doForceReset(name ?? '')} disabled={usersLoading}>
                    임시 비밀번호
                  </button>
                  {!admin && (
                    <button
                      type="button"
                      className="danger"
                      onClick={() => doDelete(name ?? '')}
                      disabled={usersLoading}
                    >
                      삭제
                    </button>
                  )}
                  {admin && <span className="hint small">관리자 계정은 삭제할 수 없습니다.</span>}
                </div>
              </li>
            )
          })}
        </ul>
      </div>

      <div className="admin-card full">
        <div className="admin-list-header">
          <h3>업로드 기록</h3>
          <button onClick={refreshLogs} disabled={logsLoading}>
            {logsLoading ? '로딩 중...' : '새로고침'}
          </button>
        </div>
        <div className="admin-logs">
          {logs.length === 0 && (
            <div className="empty">
              {logsLoading ? '로딩 중...' : '아직 업로드 기록이 없습니다.'}
            </div>
          )}
          {logs.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>시간</th>
                  <th>사용자</th>
                  <th>파일명</th>
                  <th>크기</th>
                  <th>IP</th>
                  <th>요약</th>
                  <th>상세</th>
                </tr>
              </thead>
              <tbody>
                {logs.map(log => {
                  const uploadedAt = log.UploadedAt ?? log.uploadedAt
                  const summary = log.AnalysisSummary ?? log.summary ?? ''
                  return (
                    <tr key={log.Id ?? log.id}>
                      <td>{uploadedAt ? new Date(uploadedAt).toLocaleString() : '-'}</td>
                      <td>{log.Username ?? log.username ?? 'unknown'}</td>
                      <td>{log.FileName ?? log.fileName}</td>
                      <td>{(log.FileSize ?? log.fileSize).toLocaleString()} bytes</td>
                      <td>{log.IpAddress ?? log.ipAddress ?? '-'}</td>
                      <td title={summary}>{summary}</td>
                      <td>
                        <button
                          type="button"
                          onClick={() => setSelectedLog(log)}
                        >
                          보기
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          )}
        </div>
        {selectedLog && (
          <div className="analysis-detail">
            <div className="detail-header">
              <h4>{selectedLog.FileName ?? selectedLog.fileName} 분석 상세</h4>
              <button type="button" onClick={() => setSelectedLog(null)}>닫기</button>
            </div>
            <pre>
              {(() => {
                const json = selectedLog.AnalysisJson ?? selectedLog.analysisJson ?? '{}'
                try {
                  return JSON.stringify(JSON.parse(json), null, 2)
                } catch {
                  return json
                }
              })()}
            </pre>
          </div>
        )}
      </div>
    </section>
  )
}
