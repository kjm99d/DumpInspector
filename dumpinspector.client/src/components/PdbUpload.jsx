import React, { useState } from 'react'
import { uploadPdb } from '../api'

export default function PdbUpload({ username, setMessage }) {
  const [file, setFile] = useState(null)
  const [product, setProduct] = useState('')
  const [version, setVersion] = useState('')
  const [comment, setComment] = useState('')
  const [uploading, setUploading] = useState(false)
  const [result, setResult] = useState(null)
  const [inputKey, setInputKey] = useState(() => Date.now())

  async function handleUpload() {
    if (!file) {
      setMessage('업로드할 PDB 파일을 선택하세요.')
      return
    }
    setUploading(true)
    try {
      const res = await uploadPdb(file, {
        productName: product.trim(),
        version: version.trim(),
        comment: comment.trim(),
        uploadedBy: username
      })
      setResult(res)
      setMessage(res.message ?? 'PDB가 심볼 스토어에 추가되었습니다.')
      setFile(null)
      setInputKey(Date.now())
    } catch (e) {
      setMessage(e.message)
    } finally {
      setUploading(false)
    }
  }

  return (
    <section className="pdb-upload">
      <h2>PDB Upload</h2>
      <p className="muted">
        WinDbg 분석에 필요한 심볼(.pdb) 파일을 SymStore에 등록합니다. 심볼 스토어 루트/제품명은 관리자 옵션에서 설정할 수 있으며,
        여기에서 업로드하면 즉시 심볼 경로에 반영됩니다.
      </p>
      <input
        key={inputKey}
        type="file"
        accept=".pdb"
        onChange={e => {
          const selected = e.target.files?.[0]
          setFile(selected || null)
        }}
      />

      <label>Product Name (선택, 미입력 시 기본값 사용)</label>
      <input value={product} onChange={e => setProduct(e.target.value)} placeholder="예: DumpInspector" />

      <label>Version (선택)</label>
      <input value={version} onChange={e => setVersion(e.target.value)} placeholder="예: 1.0.0.0" />

      <label>Comment (선택)</label>
      <input
        value={comment}
        onChange={e => setComment(e.target.value)}
        placeholder="예: Uploaded after hotfix"
      />

      <div className="row">
        <button onClick={handleUpload} disabled={uploading}>
          {uploading ? '업로드 중...' : 'PDB 업로드'}
        </button>
      </div>

      {result && (
        <div className="pdb-result">
          <p>
            <strong>Symbol Store</strong> <code>{result.symbolStoreRoot}</code>
          </p>
          <p>
            <strong>Product</strong> {result.product ?? '—'}{result.version && <> / v{result.version}</>}
          </p>
          <p>
            <strong>Original File</strong> {result.originalFileName}
          </p>
          <p>
            <strong>Command</strong> <code>{result.symStoreCommand}</code>
          </p>
          <label>symstore 출력</label>
          <pre>{result.symStoreOutput || '출력이 없습니다.'}</pre>
        </div>
      )}
    </section>
  )
}
