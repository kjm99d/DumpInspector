import React, { useEffect, useState } from 'react'
import { listDumps } from '../api'

export default function Dumps() {
  const [dumps, setDumps] = useState([])

  async function refresh() {
    try {
      const res = await listDumps()
      setDumps(res)
    } catch (e) {
      console.error(e)
    }
  }

  useEffect(() => { refresh() }, [])

  return (
    <section>
      <h2>Dumps</h2>
      <button onClick={refresh}>Refresh</button>
      <ul>
        {dumps.map(d => <li key={d.Name}>{d.Name} — <span className="small">{d.Size} bytes</span></li>)}
      </ul>
    </section>
  )
}
