// Login screen — mirrors login.component.ts convention: centered card, 400px,
// outlined fields with prefix icon, full-width primary button, SSO below divider.

function LoginScreen({ onLogin }) {
  const [email, setEmail] = React.useState("louis.morel@fr.ch");
  const [password, setPassword] = React.useState("");
  const [error, setError] = React.useState("");

  const submit = (e) => {
    e.preventDefault();
    if (!password) { setError("Identifiants invalides. Vérifiez votre e-mail et votre mot de passe."); return; }
    setError("");
    onLogin?.({ email, name: "Louis Morel", initials: "LM", role: "Agent LIMS" });
  };

  return (
    <div style={{
      minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center",
      background: "#f5f5f4",
      backgroundImage: "url(../../assets/bg-login-pattern.svg)",
      backgroundSize: "cover", backgroundPosition: "center",
      padding: 24,
    }}>
      <form onSubmit={submit} style={{
        width: 400, background: "#fff", borderRadius: 8,
        padding: 32, border: "1px solid #dfdfdc",
        boxShadow: "0 2px 4px rgba(17,47,66,0.06), 0 4px 8px rgba(17,47,66,0.08)",
        display: "flex", flexDirection: "column", gap: 20,
      }}>
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 10 }}>
          <img src="../../assets/mark.svg" width="48" height="48"/>
          <h1 style={{ margin: 0, fontSize: 22, fontWeight: 600 }}>Aquaplan</h1>
          <div style={{ fontSize: 13, color: "#6b6b67", textAlign: "center" }}>
            Gestion des analyses d'eau
          </div>
        </div>

        <Field label="E-mail" icon="mail_outline" value={email} onChange={setEmail}/>
        <Field label="Mot de passe" icon="lock_outline" type="password"
               value={password} onChange={setPassword}
               error={error || undefined}/>

        <Button variant="primary" fullWidth>Se connecter</Button>

        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <div style={{ flex: 1, height: 1, background: "#dfdfdc" }}/>
          <span style={{ fontSize: 12, color: "#6b6b67" }}>ou</span>
          <div style={{ flex: 1, height: 1, background: "#dfdfdc" }}/>
        </div>

        <Button variant="stroked" icon="business" fullWidth>Connexion SSO cantonale</Button>

        <div style={{ fontSize: 12, color: "#6b6b67", textAlign: "center", fontStyle: "italic" }}>
          Besoin d'un accès ? Contactez votre administrateur.
        </div>
      </form>
    </div>
  );
}

Object.assign(window, { LoginScreen });
