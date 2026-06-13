// Aquaplan layout chrome: Sidebar + TopHeader + Shell
const { useState: useStateLayout } = React;

function Sidebar({ active, onNav }) {
  const items = [
    { id: "orders",    icon: "assignment",   label: "Ordres" },
    { id: "rounds",    icon: "route",        label: "Tournées" },
    { id: "locations", icon: "place",        label: "Lieux" },
    { id: "programs",  icon: "science",      label: "Programmes" },
    { id: "results",   icon: "analytics",    label: "Résultats" },
    { id: "admin",     icon: "settings",     label: "Admin" },
  ];
  return (
    <aside style={{
      width: 240, background: "#0d3e56", color: "#fff",
      display: "flex", flexDirection: "column", flexShrink: 0,
      padding: "16px 12px", gap: 2,
    }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "4px 8px 18px" }}>
        <img src="../../assets/mark.svg" width="24" height="24"/>
        <span style={{ fontSize: 16, fontWeight: 600 }}>Aquaplan</span>
      </div>
      {items.map(it => {
        const isActive = it.id === active;
        return (
          <button key={it.id} onClick={() => onNav?.(it.id)} style={{
            display: "flex", alignItems: "center", gap: 10,
            padding: "10px 12px", borderRadius: 6, border: "none",
            background: isActive ? "rgba(255,255,255,0.10)" : "transparent",
            color: isActive ? "#fff" : "rgba(255,255,255,0.75)",
            font: "500 13px var(--font-family-base)", cursor: "pointer",
            textAlign: "left", transition: "background 120ms",
          }}
          onMouseEnter={e => { if (!isActive) e.currentTarget.style.background = "rgba(255,255,255,0.06)"; }}
          onMouseLeave={e => { if (!isActive) e.currentTarget.style.background = "transparent"; }}>
            <span className="material-icons-outlined" style={{ fontSize: 18 }}>{it.icon}</span>
            {it.label}
          </button>
        );
      })}
      <div style={{ flex: 1 }}/>
      <div style={{
        display: "flex", alignItems: "center", gap: 10,
        padding: "10px 12px", color: "rgba(255,255,255,0.75)", fontSize: 12,
        borderTop: "1px solid rgba(255,255,255,0.08)",
      }}>
        <span className="material-icons-outlined" style={{ fontSize: 18 }}>wifi</span>
        Sync à jour
      </div>
    </aside>
  );
}

function TopHeader({ user, onLogout }) {
  return (
    <header style={{
      height: 64, flexShrink: 0, display: "flex", alignItems: "center",
      padding: "0 24px", borderBottom: "1px solid #dfdfdc", background: "#fff",
      justifyContent: "space-between",
    }}>
      <div style={{ fontSize: 13, color: "#6b6b67" }}>
        Gestion des analyses d'eau
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
        <span className="material-icons-outlined" style={{ color: "#6b6b67", cursor: "pointer" }}>notifications_none</span>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <div style={{
            width: 32, height: 32, borderRadius: "50%", background: "#1f7aa6",
            color: "#fff", display: "flex", alignItems: "center", justifyContent: "center",
            fontSize: 13, fontWeight: 600,
          }}>{user?.initials ?? "LM"}</div>
          <div style={{ fontSize: 13 }}>
            <div style={{ fontWeight: 500 }}>{user?.name ?? "Louis Morel"}</div>
            <div style={{ color: "#6b6b67", fontSize: 11 }}>{user?.role ?? "Agent LIMS"}</div>
          </div>
        </div>
        <button onClick={onLogout} title="Déconnexion" style={{
          background: "transparent", border: "none", cursor: "pointer",
          color: "#6b6b67", display: "flex", alignItems: "center",
        }}><span className="material-icons-outlined">logout</span></button>
      </div>
    </header>
  );
}

function Shell({ active, onNav, user, onLogout, children }) {
  return (
    <div style={{ display: "flex", height: "100vh", background: "#f5f5f4" }}>
      <Sidebar active={active} onNav={onNav}/>
      <div style={{ flex: 1, display: "flex", flexDirection: "column", minWidth: 0 }}>
        <TopHeader user={user} onLogout={onLogout}/>
        <main style={{ flex: 1, overflow: "auto", padding: 24 }}>
          {children}
        </main>
      </div>
    </div>
  );
}

Object.assign(window, { Sidebar, TopHeader, Shell });
