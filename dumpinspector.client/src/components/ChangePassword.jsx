import React, { useState } from 'react'
import { changePassword } from '../api'

export default function ChangePassword({ username, setMessage }) {
  const [oldP, setOldP] = useState('')
  const [newP, setNewP] = useState('')

  async function doChange() {
    try {
      await changePassword(username, oldP, newP)
      setMessage('Password changed')
    } catch (e) { setMessage(e.message) }
  }

  return (
    <section>
      <h2>Change Password</h2>
      <input placeholder="old password" type="password" value={oldP} onChange={e => setOldP(e.target.value)} />
      <input placeholder="new password" type="password" value={newP} onChange={e => setNewP(e.target.value)} />
      <div className="row"><button onClick={doChange}>Change</button></div>
    </section>
  )
}
