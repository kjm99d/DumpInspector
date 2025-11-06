import React, { useEffect, useRef, useState } from 'react'
import { uploadDump } from '../api'

export default function Upload({ username, setMessage }) {
  const [file, setFile] = useState(null)
  const [sessionInfo, setSessionInfo] = useState(null)
  const [streamLines, setStreamLines] = useState([])
  const [finalResult, setFinalResult] = useState(null)
  const [isStreaming, setIsStreaming] = useState(false)
  const socketRef = useRef(null)

  useEffect(() => {
    return () => {
      if (socketRef.current) {
        socketRef.current.close()
        socketRef.current = null
      }
    }
  }, [])

  function startStream(sessionId) {
    if (!sessionId) return

    if (socketRef.current) {
      socketRef.current.close()
      socketRef.current = null
    }

    const protocol = window.location.protocol === 'https:' ? 'wss' : 'ws'
    const wsUrl = `${protocol}://${window.location.host}/ws/analysis?id=${encodeURIComponent(sessionId)}`
    const ws = new WebSocket(wsUrl)
    socketRef.current = ws

    setStreamLines([])
    setFinalResult(null)
    setIsStreaming(true)

    ws.onmessage = event => {
      try {
        const payload = JSON.parse(event.data)
        if (payload.type === 'line' || payload.type === 'info') {
          const data = payload.data ?? payload.message
          if (!data) return
          setStreamLines(prev => {
            const next = [...prev, data]
            return next.slice(-2000)
          })
        } else if (payload.type === 'complete') {
          setFinalResult({
            summary: payload.summary,
            detailedReport: payload.detailedReport,
            analyzedAt: payload.analyzedAt,
            fileName: payload.fileName
          })
          setMessage('분석이 완료되었습니다.')
          setIsStreaming(false)
          ws.close()
          socketRef.current = null
        } else if (payload.type === 'error') {
          setMessage(payload.message || '분석 중 오류가 발생했습니다.')
          setIsStreaming(false)
          ws.close()
          socketRef.current = null
        }
      } catch (err) {
        console.error('Streaming parse error', err)
      }
    }

    ws.onerror = () => {
      setMessage('실시간 분석 스트림에 연결할 수 없습니다.')
      setIsStreaming(false)
    }

    ws.onclose = () => {
      socketRef.current = null
      setIsStreaming(false)
    }
  }

  async function doUpload() {
    if (!file) return setMessage('업로드할 파일을 선택하세요.')
    try {
      const res = await uploadDump(file, username)
      if (!res || !res.sessionId) {
        setMessage('세션 정보를 가져오지 못했습니다.')
        return
      }
      setMessage('업로드가 완료되었습니다. 분석을 시작합니다...')
      setSessionInfo(res)
      startStream(res.sessionId)
    } catch (e) {
      setSessionInfo(null)
      setStreamLines([])
      setFinalResult(null)
      setIsStreaming(false)
      setMessage(e.message)
    }
  }

  function handleCopyReport() {
    if (!finalResult?.detailedReport) return
    navigator.clipboard?.writeText(finalResult.detailedReport)
      .then(() => setMessage('리포트를 클립보드에 복사했습니다.'))
      .catch(() => setMessage('리포트를 복사하지 못했습니다. 브라우저 권한을 확인하세요.'))
  }

  return (
    <section>
      <h2>Upload Dump</h2>
      <input type="file" onChange={e => setFile(e.target.files[0])} />
      <div className="row"><button onClick={doUpload}>Upload</button></div>

      {sessionInfo && (
        <div className="small">
          <div><strong>File:</strong> {sessionInfo.fileName}</div>
          <div><strong>Size:</strong> {sessionInfo.sizeBytes} bytes</div>
        </div>
      )}

      {isStreaming && (
        <div className="analysis-stream">
          <strong>실시간 출력</strong>
          <pre>{streamLines.join('\n')}</pre>
        </div>
      )}

      {finalResult && (
        <div className="analysis-result">
          <div><strong>Summary:</strong> {finalResult.summary}</div>
          <div><strong>Analyzed:</strong> {finalResult.analyzedAt ? new Date(finalResult.analyzedAt).toLocaleString() : '-'}</div>
          {finalResult.detailedReport && (
            <div className="analysis-output large">
              <button type="button" className="copy-btn overlay" onClick={handleCopyReport}>
                <span className="copy-icon">📋</span> 복사
              </button>
              <pre>{finalResult.detailedReport}</pre>
            </div>
          )}
        </div>
      )}
    </section>
  )
}
