// Round detail — the field-sampling view. Mobile-shaped, but usable on desktop too.
// Demonstrates: page header with back, list of sampling locations as cards,
// per-card status chip, scan CTA, offline banner.

function RoundDetailPage({ onBack }) {
  const [locations, setLocations] = React.useState([
    { id: 1, name: "Source La Sarine",    code: "LSA-01", status: "sampled",     time: "08:42" },
    { id: 2, name: "Réservoir Bulle",     code: "BUL-02", status: "in_progress", time: "—" },
    { id: 3, name: "Piscine Marly",       code: "MAR-04", status: "planned",     time: "—" },
    { id: 4, name: "Fontaine Estavayer",  code: "EST-07", status: "planned",     time: "—" },
    { id: 5, name: "Source Lavapesson",   code: "LAV-03", status: "planned",     time: "—" },
  ]);

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 10 }}>
        <button onClick={onBack} style={{
          background: "transparent", border: "none", cursor: "pointer",
          display: "flex", alignItems: "center", gap: 4, color: "#6b6b67", fontSize: 13,
        }}>
          <span className="material-icons-outlined" style={{ fontSize: 18 }}>arrow_back</span>
          Tournées
        </button>
      </div>
      <PageHeader title="Tournée 24.04 — secteur Nord"
        count="Louis Morel · 5 lieux"
        action={<Button icon="qr_code_scanner" size="lg">Scanner un échantillon</Button>}/>

      {/* Offline banner */}
      <div style={{
        display: "flex", alignItems: "center", gap: 10,
        padding: "10px 14px", marginBottom: 16,
        background: "#fcefcf", color: "#855d08",
        border: "1px solid #f0dca1", borderRadius: 8, fontSize: 13,
      }}>
        <span className="material-icons-outlined" style={{ fontSize: 18 }}>cloud_off</span>
        Mode hors-ligne — 3 prélèvements en attente de synchronisation.
      </div>

      <SectionLabel>Lieux de prélèvement</SectionLabel>
      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {locations.map(l => (
          <Card key={l.id} clickable style={{ padding: 14 }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 12, minWidth: 0 }}>
                <div style={{
                  width: 40, height: 40, borderRadius: 8, background: "#eff7fb",
                  display: "flex", alignItems: "center", justifyContent: "center",
                  flexShrink: 0,
                }}>
                  <span className="material-icons-outlined" style={{ color: "#166389" }}>place</span>
                </div>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontSize: 15, fontWeight: 500, marginBottom: 2, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{l.name}</div>
                  <div style={{ fontSize: 12, color: "#6b6b67", fontFamily: "var(--font-family-mono)" }}>
                    {l.code}{l.time !== "—" && ` · prélevé à ${l.time}`}
                  </div>
                </div>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <StatusChip status={l.status}/>
                <span className="material-icons-outlined" style={{ color: "#9a9a96" }}>chevron_right</span>
              </div>
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}

Object.assign(window, { RoundDetailPage });
