// Orders list — the quintessential Aquaplan screen. Filters row + data table
// + page header with action, per the "liste paginée filtrable" pattern.

const ORDERS_SEED = [
  { id: "A-2041", loc: "Source La Sarine",        program: "P-12", status: "in_progress", date: "18.04.2026", sampler: "L. Morel" },
  { id: "A-2042", loc: "Réservoir Bulle",         program: "P-08", status: "sampled",     date: "19.04.2026", sampler: "L. Morel" },
  { id: "A-2043", loc: "Piscine Marly",           program: "P-04", status: "to_plan",     date: "—",          sampler: "—" },
  { id: "A-2044", loc: "Lac Noir",                program: "P-15", status: "results",     date: "21.04.2026", sampler: "C. Rime" },
  { id: "A-2045", loc: "Source Lavapesson",       program: "P-12", status: "planned",     date: "24.04.2026", sampler: "L. Morel" },
  { id: "A-2046", loc: "Piscine de la Motta",     program: "P-04", status: "in_analysis", date: "17.04.2026", sampler: "C. Rime" },
  { id: "A-2047", loc: "Réservoir Romont",        program: "P-08", status: "closed",      date: "10.04.2026", sampler: "L. Morel" },
  { id: "A-2048", loc: "Fontaine Estavayer",      program: "P-02", status: "cancelled",   date: "15.04.2026", sampler: "—" },
  { id: "A-2049", loc: "Source Charmey",          program: "P-12", status: "planned",     date: "25.04.2026", sampler: "C. Rime" },
  { id: "A-2050", loc: "Lac de Schiffenen",       program: "P-15", status: "in_progress", date: "22.04.2026", sampler: "L. Morel" },
];

function OrderListPage({ onOpenOrder, onCreateOrder }) {
  const [query, setQuery] = React.useState("");
  const [statusFilter, setStatusFilter] = React.useState("all");
  const [selected, setSelected] = React.useState(null);

  const filtered = ORDERS_SEED.filter(o => {
    if (statusFilter !== "all" && o.status !== statusFilter) return false;
    if (query && !(o.id + o.loc + o.program).toLowerCase().includes(query.toLowerCase())) return false;
    return true;
  });

  const filterPill = (id, label) => {
    const isActive = statusFilter === id;
    return (
      <button key={id} onClick={() => setStatusFilter(id)} style={{
        height: 32, padding: "0 12px", borderRadius: 16,
        border: isActive ? "1px solid #1f7aa6" : "1px solid #dfdfdc",
        background: isActive ? "#eff7fb" : "#fff",
        color: isActive ? "#114f6e" : "#4f4f4c",
        font: "500 13px var(--font-family-base)", cursor: "pointer",
      }}>{label}</button>
    );
  };

  return (
    <div>
      <PageHeader title="Ordres d'analyse" count={`${filtered.length} sur ${ORDERS_SEED.length}`}
        action={<Button icon="add" onClick={onCreateOrder}>Nouvel ordre</Button>}/>

      {/* Filters row */}
      <div style={{ display: "flex", gap: 10, marginBottom: 16, alignItems: "center", flexWrap: "wrap" }}>
        <div style={{ flex: "0 0 280px" }}>
          <Field icon="search" placeholder="Rechercher un code ou un lieu" value={query} onChange={setQuery}/>
        </div>
        {filterPill("all",         "Tous")}
        {filterPill("to_plan",     "À planifier")}
        {filterPill("planned",     "Planifié")}
        {filterPill("in_progress", "En cours")}
        {filterPill("sampled",     "Prélevé")}
        {filterPill("results",     "Résultats")}
      </div>

      {/* Table */}
      <div style={{
        background: "#fff", border: "1px solid #dfdfdc", borderRadius: 8, overflow: "hidden",
      }}>
        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
          <thead>
            <tr>
              {["Code", "Lieu de prélèvement", "Programme", "Préleveur", "Statut", "Date prévue", ""].map((h, i) =>
                <th key={i} style={{
                  textAlign: "left", fontWeight: 500, color: "#6b6b67",
                  fontSize: 12, padding: "10px 14px",
                  borderBottom: "1px solid #dfdfdc", background: "#fafaf9",
                }}>{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {filtered.map(o => (
              <tr key={o.id}
                  onClick={() => { setSelected(o.id); onOpenOrder?.(o); }}
                  style={{
                    cursor: "pointer",
                    background: selected === o.id ? "rgba(31,122,166,0.12)" : "transparent",
                  }}
                  onMouseEnter={e => { if (selected !== o.id) e.currentTarget.style.background = "rgba(17,79,110,0.05)"; }}
                  onMouseLeave={e => { if (selected !== o.id) e.currentTarget.style.background = "transparent"; }}>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", fontFamily: "var(--font-family-mono)", fontWeight: 500 }}>{o.id}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea" }}>{o.loc}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", color: "#6b6b67" }}>{o.program}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", color: "#6b6b67" }}>{o.sampler}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea" }}><StatusChip status={o.status}/></td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", color: "#6b6b67", fontFamily: "var(--font-family-mono)" }}>{o.date}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", textAlign: "right" }}>
                  <span className="material-icons-outlined" style={{ fontSize: 18, color: "#9a9a96" }}>chevron_right</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div style={{
          padding: "8px 14px", fontSize: 12, color: "#6b6b67",
          borderTop: "1px solid #dfdfdc", display: "flex", justifyContent: "space-between", alignItems: "center",
          background: "#fafaf9",
        }}>
          <div>Affichage 1–{filtered.length} sur {filtered.length}</div>
          <div style={{ display: "flex", gap: 4 }}>
            <button style={{ width: 32, height: 32, border: "1px solid #dfdfdc", background: "#fff", borderRadius: 4, cursor: "pointer" }}>
              <span className="material-icons-outlined" style={{ fontSize: 16 }}>chevron_left</span>
            </button>
            <button style={{ width: 32, height: 32, border: "1px solid #dfdfdc", background: "#fff", borderRadius: 4, cursor: "pointer" }}>
              <span className="material-icons-outlined" style={{ fontSize: 16 }}>chevron_right</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { OrderListPage, ORDERS_SEED });
