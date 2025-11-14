const BASE = '/api'

async function request(path, opts = {}) {
  const res = await fetch(`${BASE}${path}`, opts)
  if (!res.ok) {
    const txt = await res.text()
    throw new Error(txt || res.statusText)
  }
  const ct = res.headers.get('content-type') || ''
  if (ct.includes('application/json')) return res.json()
  return res.text()
}

export async function validate(username, password) {
  return request('/auth/validate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password })
  })
}

export async function isAdmin(username) {
  return request(`/auth/is-admin/${encodeURIComponent(username)}`)
}

export async function changePassword(username, oldPassword, newPassword) {
  return request('/auth/change-password', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, oldPassword, newPassword })
  })
}

export async function uploadDump(file, username) {
  const fd = new FormData()
  fd.append('file', file)
  if (username) fd.append('uploadedBy', username)
  return request('/dump/upload', { method: 'POST', body: fd })
}

export async function listDumps() {
  return request('/dump/list')
}

export async function getOptions() {
  return request('/options/CrashDumpSettings')
}

export async function saveOptions(obj) {
  return request('/options/CrashDumpSettings', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(obj) })
}

export async function adminCreateUser(username, email) {
  const headers = { 'Content-Type': 'application/json' }
  return request('/admin/create-user', {
    method: 'POST',
    headers,
    body: JSON.stringify({ username, email })
  })
}

export async function adminListUsers() {
  return request('/admin/users')
}

export async function adminForceReset(username) {
  const headers = { 'Content-Type': 'application/json' }
  return request('/admin/force-reset', {
    method: 'POST',
    headers,
    body: JSON.stringify({ username })
  })
}

export async function adminDeleteUser(username) {
  return request(`/admin/users/${encodeURIComponent(username)}`, {
    method: 'DELETE'
  })
}

export async function adminGetLogs(take = 100) {
  return request(`/admin/logs?take=${encodeURIComponent(take)}`)
}

export async function uploadPdb(file, { productName, version, comment, uploadedBy } = {}) {
  if (!file) throw new Error('업로드할 PDB 파일을 선택하세요.')
  const fd = new FormData()
  fd.append('file', file)
  if (productName) fd.append('productName', productName)
  if (version) fd.append('version', version)
  if (comment) fd.append('comment', comment)
  if (uploadedBy) fd.append('uploadedBy', uploadedBy)
  return request('/pdb/upload', {
    method: 'POST',
    body: fd
  })
}
