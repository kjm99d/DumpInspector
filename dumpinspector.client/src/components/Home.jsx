import React from 'react'

export default function Home({ isAdmin }) {
  return (
    <section className="home">
      <h2>Welcome back</h2>
      <p className="home-intro">
        Use the navigation above to upload new crash dumps, review previous analyses, and manage your account.
      </p>

      <div className="home-grid">
        <div className="home-card">
          <h3>Upload &amp; Inspect</h3>
          <p>Send a fresh dump file to the server and instantly receive a short analysis summary.</p>
        </div>
        <div className="home-card">
          <h3>History</h3>
          <p>Visit the Dumps tab to review files already stored on the server, including their sizes.</p>
        </div>
        <div className="home-card">
          <h3>Account</h3>
          <p>Change your password anytime to keep access secure.</p>
        </div>
        {isAdmin && (
          <div className="home-card">
            <h3>Administration</h3>
            <p>Adjust crash dump settings, manage user accounts, and control NAS access from the admin tabs.</p>
          </div>
        )}
      </div>
    </section>
  )
}
