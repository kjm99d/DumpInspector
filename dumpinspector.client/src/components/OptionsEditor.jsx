import React, { useEffect, useState } from 'react'

export default function OptionsEditor({ loadOptions, saveOptions, setMessage, embedded = false }) {
  const [loaded, setLoaded] = useState(false)
  const [dumpStoragePath, setDumpStoragePath] = useState('Dumps')
  const [useNas, setUseNas] = useState(false)
  const [nasBaseUrl, setNasBaseUrl] = useState('')
  const [nasUsername, setNasUsername] = useState('')
  const [nasPassword, setNasPassword] = useState('')
  const [nasRemotePath, setNasRemotePath] = useState('pdb')
  const [smtpEnabled, setSmtpEnabled] = useState(false)
  const [smtpHost, setSmtpHost] = useState('')
  const [smtpPort, setSmtpPort] = useState('587')
  const [smtpUseSsl, setSmtpUseSsl] = useState(true)
  const [smtpUsername, setSmtpUsername] = useState('')
  const [smtpPassword, setSmtpPassword] = useState('')
  const [smtpFrom, setSmtpFrom] = useState('')
  const [cdbPath, setCdbPath] = useState('')
  const [symbolPath, setSymbolPath] = useState('')
  const [symStorePath, setSymStorePath] = useState('')
  const [symbolStoreRoot, setSymbolStoreRoot] = useState('Symbols')
  const [symbolStoreProduct, setSymbolStoreProduct] = useState('DumpInspector')
  const [dumpUploadLimitMb, setDumpUploadLimitMb] = useState('10240')
  const [analysisTimeout, setAnalysisTimeout] = useState('120')

  useEffect(() => {
    let cancelled = false
    async function fetchOptions() {
      try {
        const opt = await loadOptions()
        if (cancelled) return
        if (opt) {
          const dumpPath = opt.DumpStoragePath ?? opt.dumpStoragePath ?? 'Dumps'
          const useNasFlag = opt.UseNasForPdb ?? opt.useNasForPdb ?? false
          const nas = opt.Nas ?? opt.nas ?? {}
          const smtp = opt.Smtp ?? opt.smtp ?? {}

          setDumpStoragePath(dumpPath)
          setUseNas(Boolean(useNasFlag))
          setNasBaseUrl(nas.BaseUrl ?? nas.baseUrl ?? '')
          setNasUsername(nas.Username ?? nas.username ?? '')
          setNasPassword(nas.Password ?? nas.password ?? '')
          setNasRemotePath(nas.RemotePdbPath ?? nas.remotePdbPath ?? 'pdb')

          const enabled =
            typeof smtp.Enabled === 'boolean'
              ? smtp.Enabled
              : typeof smtp.enabled === 'boolean'
                ? smtp.enabled
                : undefined
          const host = smtp.Host ?? smtp.host ?? ''

          setSmtpEnabled(enabled ?? (typeof host === 'string' && host.trim() !== ''))
          setSmtpHost(host)
          setSmtpPort(String(smtp.Port ?? smtp.port ?? 587))
          setSmtpUseSsl(smtp.UseSsl ?? smtp.useSsl ?? true)
          setSmtpUsername(smtp.Username ?? smtp.username ?? '')
          setSmtpPassword(smtp.Password ?? smtp.password ?? '')
          setSmtpFrom(smtp.FromAddress ?? smtp.fromAddress ?? '')
          setCdbPath(opt.CdbPath ?? opt.cdbPath ?? '')
          setSymbolPath(opt.SymbolPath ?? opt.symbolPath ?? '')
          setSymStorePath(opt.SymStorePath ?? opt.symStorePath ?? '')
          setSymbolStoreRoot(opt.SymbolStoreRoot ?? opt.symbolStoreRoot ?? 'Symbols')
          setSymbolStoreProduct(opt.SymbolStoreProduct ?? opt.symbolStoreProduct ?? 'DumpInspector')
          const limitBytes = Number(opt.DumpUploadMaxBytes ?? opt.dumpUploadMaxBytes ?? (10 * 1024 * 1024 * 1024))
          const limitMb = limitBytes > 0 ? Math.round(limitBytes / (1024 * 1024)) : 10240
          setDumpUploadLimitMb(String(limitMb))
          setAnalysisTimeout(String(parseInt(opt.AnalysisTimeoutSeconds ?? opt.analysisTimeoutSeconds ?? 120, 10) || 120))
        }
        setLoaded(true)
      } catch (e) {
        setMessage(e.message)
      }
    }
    fetchOptions()
    return () => { cancelled = true }
  }, [loadOptions, setMessage])

  async function doSave() {
    try {
      const trimmedHost = smtpHost.trim()
      if (smtpEnabled && trimmedHost === '') {
        setMessage('SMTP를 사용하려면 호스트를 입력하세요.')
        return
      }
      const obj = {
        DumpStoragePath: dumpStoragePath,
        UseNasForPdb: useNas,
        Nas: {
          BaseUrl: nasBaseUrl,
          Username: nasUsername,
          Password: nasPassword,
          RemotePdbPath: nasRemotePath
        },
        Smtp: {
          Enabled: smtpEnabled,
          Host: trimmedHost,
          Port: Number(smtpPort) || 0,
          UseSsl: smtpUseSsl,
          Username: smtpUsername,
          Password: smtpPassword,
          FromAddress: smtpFrom.trim()
        },
        CdbPath: cdbPath,
        SymbolPath: symbolPath,
        SymStorePath: symStorePath,
        SymbolStoreRoot: symbolStoreRoot,
        SymbolStoreProduct: symbolStoreProduct,
        DumpUploadMaxBytes: Math.max(1, Number(dumpUploadLimitMb) || 0) * 1024 * 1024,
        AnalysisTimeoutSeconds: Number(analysisTimeout) || 120
      }
      await saveOptions(obj)
      setMessage('옵션이 저장되었습니다.')
    } catch (e) { setMessage(e.message) }
  }

  if (!loaded) return <div>로딩 중...</div>

  const content = (
    <>
      {!embedded && <h2>Admin Options</h2>}
      <label>Dump storage path</label>
      <input value={dumpStoragePath} onChange={e => setDumpStoragePath(e.target.value)} />

      <div className="options-checkbox">
        <input id="use-nas-toggle" type="checkbox" checked={useNas} onChange={e => setUseNas(e.target.checked)} />
        <label htmlFor="use-nas-toggle">Use NAS for PDB</label>
      </div>

     {useNas && (
       <div className="nas-config">
         <label>NAS Base URL</label>
         <input value={nasBaseUrl} onChange={e => setNasBaseUrl(e.target.value)} />
          <label>NAS Username</label>
          <input value={nasUsername} onChange={e => setNasUsername(e.target.value)} />
          <label>NAS Password</label>
          <input type="password" value={nasPassword} onChange={e => setNasPassword(e.target.value)} />
          <label>NAS Remote PDB Path</label>
          <input value={nasRemotePath} onChange={e => setNasRemotePath(e.target.value)} />
        </div>
      )}

      <div className="options-checkbox">
        <input
          id="use-smtp-toggle"
          type="checkbox"
          checked={smtpEnabled}
          onChange={e => setSmtpEnabled(e.target.checked)}
        />
        <label htmlFor="use-smtp-toggle">SMTP 설정 사용</label>
      </div>

      {smtpEnabled && (
        <div className="smtp-config">
          <label>SMTP Host</label>
          <input value={smtpHost} onChange={e => setSmtpHost(e.target.value)} />
          <label>SMTP Port</label>
          <input type="number" value={smtpPort} onChange={e => setSmtpPort(e.target.value)} />
          <div className="options-checkbox small">
            <input id="smtp-ssl" type="checkbox" checked={smtpUseSsl} onChange={e => setSmtpUseSsl(e.target.checked)} />
            <label htmlFor="smtp-ssl">SSL/TLS 사용</label>
          </div>
          <label>SMTP Username</label>
          <input value={smtpUsername} onChange={e => setSmtpUsername(e.target.value)} />
          <label>SMTP Password</label>
          <input type="password" value={smtpPassword} onChange={e => setSmtpPassword(e.target.value)} />
          <label>From Address</label>
          <input type="email" value={smtpFrom} onChange={e => setSmtpFrom(e.target.value)} />
        </div>
      )}

      <label>CDB Path (optional)</label>
      <input value={cdbPath} onChange={e => setCdbPath(e.target.value)} placeholder="예: C:\\Program Files\\Windows Kits\\10\\Debuggers\\x64\\cdb.exe" />

      <label>Symbol Path (optional)</label>
      <input value={symbolPath} onChange={e => setSymbolPath(e.target.value)} placeholder="예: srv*C:\\symbols*https://msdl.microsoft.com/download/symbols" />

      <label>SymStore Path (optional)</label>
      <input
        value={symStorePath}
        onChange={e => setSymStorePath(e.target.value)}
        placeholder="예: C:\\Program Files (x86)\\Windows Kits\\10\\Debuggers\\x64\\symstore.exe"
      />

      <label>Symbol Store Root</label>
      <input
        value={symbolStoreRoot}
        onChange={e => setSymbolStoreRoot(e.target.value)}
        placeholder="예: C:\\symbols"
      />

      <label>Symbol Store Product Name</label>
      <input
        value={symbolStoreProduct}
        onChange={e => setSymbolStoreProduct(e.target.value)}
        placeholder="예: DumpInspector"
      />

      <label>Analysis Timeout (seconds)</label>
      <input type="number" value={analysisTimeout} onChange={e => setAnalysisTimeout(e.target.value)} />

      <div className="row">
        <button onClick={doSave}>Save</button>
      </div>
    </>
  )

  return embedded ? content : <section>{content}</section>
}
      <label>Dump Upload Limit (MB)</label>
      <input
        type="number"
        min="1"
        value={dumpUploadLimitMb}
        onChange={e => setDumpUploadLimitMb(e.target.value)}
      />
