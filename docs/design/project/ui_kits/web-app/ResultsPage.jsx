// Results page — demonstrates sampling-results-table.

function ResultsPage() {
  const params = [
    { code: "E. coli",      value: "0",      unit: "UFC/100 mL", limit: "< 1",     status: "conform" },
    { code: "Entérocoques", value: "0",      unit: "UFC/100 mL", limit: "< 1",     status: "conform" },
    { code: "Nitrate NO₃⁻", value: "28.4",   unit: "mg/L",       limit: "≤ 40",    status: "conform" },
    { code: "Nitrite NO₂⁻", value: "0.12",   unit: "mg/L",       limit: "≤ 0.10",  status: "non_conform" },
    { code: "Chlore libre", value: "0.18",   unit: "mg/L",       limit: "0.1–0.3", status: "conform" },
    { code: "pH",           value: "7.8",    unit: "",           limit: "6.8–8.2", status: "conform" },
    { code: "Turbidité",    value: "0.4",    unit: "NTU",        limit: "≤ 1",     status: "conform" },
    { code: "Conductivité", value: "412",    unit: "µS/cm",      limit: "—",       status: "pending" },
  ];

  return (
    <div>
      <PageHeader title="Résultats — A-2044" count="Lac Noir · P-15 · 21.04.2026"
        action={<div style={{ display: "flex", gap: 8 }}>
          <Button variant="stroked" icon="file_download">Exporter PDF</Button>
          <Button variant="primary" icon="check">Valider</Button>
        </div>}/>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12, marginBottom: 20 }}>
        <Card>
          <SectionLabel>Synthèse</SectionLabel>
          <div style={{ display: "flex", alignItems: "baseline", gap: 8 }}>
            <div style={{ fontSize: 32, fontWeight: 600, color: "#1e5c32" }}>7</div>
            <div style={{ color: "#6b6b67", fontSize: 13 }}>conformes sur 8</div>
          </div>
        </Card>
        <Card>
          <SectionLabel>Non conformes</SectionLabel>
          <div style={{ display: "flex", alignItems: "baseline", gap: 8 }}>
            <div style={{ fontSize: 32, fontWeight: 600, color: "#973057" }}>1</div>
            <div style={{ color: "#6b6b67", fontSize: 13 }}>Nitrite NO₂⁻</div>
          </div>
        </Card>
        <Card>
          <SectionLabel>En attente</SectionLabel>
          <div style={{ display: "flex", alignItems: "baseline", gap: 8 }}>
            <div style={{ fontSize: 32, fontWeight: 600, color: "#855d08" }}>1</div>
            <div style={{ color: "#6b6b67", fontSize: 13 }}>Conductivité</div>
          </div>
        </Card>
      </div>

      <SectionLabel>Paramètres mesurés</SectionLabel>
      <div style={{ background: "#fff", border: "1px solid #dfdfdc", borderRadius: 8, overflow: "hidden" }}>
        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
          <thead>
            <tr>
              {["Paramètre", "Valeur", "Unité", "Valeur limite", "Statut"].map((h, i) =>
                <th key={i} style={{ textAlign: "left", fontWeight: 500, color: "#6b6b67", fontSize: 12, padding: "10px 14px", borderBottom: "1px solid #dfdfdc", background: "#fafaf9" }}>{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {params.map((p, i) => (
              <tr key={i}>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", fontWeight: 500 }}>{p.code}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", fontFamily: "var(--font-family-mono)" }}>{p.value}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", color: "#6b6b67" }}>{p.unit}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea", color: "#6b6b67", fontFamily: "var(--font-family-mono)" }}>{p.limit}</td>
                <td style={{ padding: "12px 14px", borderBottom: "1px solid #ececea" }}><StatusChip status={p.status}/></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

Object.assign(window, { ResultsPage });
