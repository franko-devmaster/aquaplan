#!/bin/sh
# Seed script for AquaPlan Synology instance — Canton de Vaud (SCAV)
# Run inside aquaplan-api Docker container terminal on Synology DSM
# API is at localhost:8080 from inside the container

set -e

API="http://localhost:8080/api"

# Install curl if not available
if ! command -v curl >/dev/null 2>&1; then
  echo "Installing curl..."
  apt-get update -qq && apt-get install -y -qq curl >/dev/null 2>&1
fi

echo "=== LOGIN ==="
TOKEN=$(curl -s -X POST "$API/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@aquaplan.ch","password":"Admin123!"}' | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')

if [ -z "$TOKEN" ]; then
  echo "ERREUR: Login echoue"; exit 1
fi
echo "OK: Token obtenu"

post() {
  local path="$1" data="$2"
  local resp code
  resp=$(curl -s -w "\n%{http_code}" -X POST "$API/$path" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "$data")
  code=$(echo "$resp" | tail -1)
  body=$(echo "$resp" | sed '$d')
  if [ "$code" = "200" ] || [ "$code" = "201" ]; then
    echo "  OK: $path"
    echo "$body"
  else
    echo "  FAIL ($code): $path"
    echo ""
  fi
}

get() {
  curl -s -X GET "$API/$1" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json"
}

# Extract ID from JSON response (simple grep)
getid() { echo "$1" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1; }

# =============================================
# 1. DISTRIBUTORS
# =============================================
echo ""
echo "=== DISTRIBUTEURS (Canton de Vaud) ==="

R=$(post "distributors" '{"name":"Service de l'\''eau de Lausanne (eauservice)","cantonRegion":"Lausanne","distributionNetwork":"Reseau urbain Lausanne - 145000 habitants"}')
D_LAUSANNE=$(getid "$R")

R=$(post "distributors" '{"name":"Service des eaux de Montreux-Veytaux","cantonRegion":"Riviera","distributionNetwork":"Reseau communal Montreux - 26000 habitants"}')
D_MONTREUX=$(getid "$R")

R=$(post "distributors" '{"name":"Association intercommunale pour l'\''epuration de Nyon (AIEN)","cantonRegion":"Nyon","distributionNetwork":"Reseau intercommunal Nyon - 22000 habitants"}')
D_NYON=$(getid "$R")

R=$(post "distributors" '{"name":"Service industriel de Vevey (SIV)","cantonRegion":"Riviera","distributionNetwork":"Reseau communal Vevey - 19500 habitants"}')
D_VEVEY=$(getid "$R")

R=$(post "distributors" '{"name":"Commune d'\''Yverdon-les-Bains - Service des eaux","cantonRegion":"Nord vaudois","distributionNetwork":"Reseau communal Yverdon - 30000 habitants"}')
D_YVERDON=$(getid "$R")

echo "  Lausanne=$D_LAUSANNE"
echo "  Montreux=$D_MONTREUX"
echo "  Nyon=$D_NYON"
echo "  Vevey=$D_VEVEY"
echo "  Yverdon=$D_YVERDON"

# If IDs are empty, try fetching existing distributors
if [ -z "$D_LAUSANNE" ]; then
  echo "  Distributeurs existants, recuperation des IDs..."
  ALL_DIST=$(get "distributors")
  D_LAUSANNE=$(echo "$ALL_DIST" | sed -n 's/.*"id":"\([^"]*\)"[^}]*"name":"Service de l.eau de Lausanne[^"]*".*/\1/p' | head -1)
  D_MONTREUX=$(echo "$ALL_DIST" | sed -n 's/.*"id":"\([^"]*\)"[^}]*"name":"Service des eaux de Montreux[^"]*".*/\1/p' | head -1)
  D_NYON=$(echo "$ALL_DIST" | sed -n 's/.*"id":"\([^"]*\)"[^}]*"name":"Association intercommunale[^"]*".*/\1/p' | head -1)
  D_VEVEY=$(echo "$ALL_DIST" | sed -n 's/.*"id":"\([^"]*\)"[^}]*"name":"Service industriel de Vevey[^"]*".*/\1/p' | head -1)
  D_YVERDON=$(echo "$ALL_DIST" | sed -n 's/.*"id":"\([^"]*\)"[^}]*"name":"Commune d.Yverdon[^"]*".*/\1/p' | head -1)
  # Fallback: extract all IDs in order if pattern matching fails
  if [ -z "$D_LAUSANNE" ]; then
    echo "  Extraction par position..."
    ALL_IDS=$(echo "$ALL_DIST" | grep -o '"id":"[^"]*"' | sed 's/"id":"//;s/"//')
    D_LAUSANNE=$(echo "$ALL_IDS" | sed -n '1p')
    D_MONTREUX=$(echo "$ALL_IDS" | sed -n '2p')
    D_NYON=$(echo "$ALL_IDS" | sed -n '3p')
    D_VEVEY=$(echo "$ALL_IDS" | sed -n '4p')
    D_YVERDON=$(echo "$ALL_IDS" | sed -n '5p')
  fi
  echo "  Lausanne=$D_LAUSANNE"
  echo "  Montreux=$D_MONTREUX"
  echo "  Nyon=$D_NYON"
  echo "  Vevey=$D_VEVEY"
  echo "  Yverdon=$D_YVERDON"
fi

TENANT="00000000-0000-0000-0000-000000000001"

# =============================================
# 2. USERS
# =============================================
echo ""
echo "=== UTILISATEURS ==="

post "users" "{\"email\":\"f.blanc@lausanne.ch\",\"firstName\":\"Francois\",\"lastName\":\"Blanc\",\"organization\":\"eauservice Lausanne\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Requ\u00e9rant\"],\"distributorIds\":[\"$D_LAUSANNE\"]}"

post "users" "{\"email\":\"m.rochat@montreux.ch\",\"firstName\":\"Michel\",\"lastName\":\"Rochat\",\"organization\":\"Commune de Montreux\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Requ\u00e9rant\"],\"distributorIds\":[\"$D_MONTREUX\"]}"

post "users" "{\"email\":\"i.martin@nyon.ch\",\"firstName\":\"Isabelle\",\"lastName\":\"Martin\",\"organization\":\"AIEN Nyon\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Requ\u00e9rant\"],\"distributorIds\":[\"$D_NYON\"]}"

post "users" "{\"email\":\"d.bonvin@vevey.ch\",\"firstName\":\"Daniel\",\"lastName\":\"Bonvin\",\"organization\":\"SIV Vevey\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Requ\u00e9rant\"],\"distributorIds\":[\"$D_VEVEY\"]}"

post "users" "{\"email\":\"c.dupont@yverdon.ch\",\"firstName\":\"Claire\",\"lastName\":\"Dupont\",\"organization\":\"Commune d'Yverdon-les-Bains\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Requ\u00e9rant\"],\"distributorIds\":[\"$D_YVERDON\"]}"

post "users" "{\"email\":\"p.favre@scav.vd.ch\",\"firstName\":\"Philippe\",\"lastName\":\"Favre\",\"organization\":\"SCAV - Canton de Vaud\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Pr\u00e9leveur\"],\"distributorIds\":[\"$D_LAUSANNE\",\"$D_MONTREUX\",\"$D_VEVEY\"]}"

post "users" "{\"email\":\"v.rouge@scav.vd.ch\",\"firstName\":\"Valerie\",\"lastName\":\"Rouge\",\"organization\":\"SCAV - Canton de Vaud\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Pr\u00e9leveur\"],\"distributorIds\":[\"$D_NYON\",\"$D_YVERDON\"]}"

post "users" "{\"email\":\"a.jaccard@scav.vd.ch\",\"firstName\":\"Alain\",\"lastName\":\"Jaccard\",\"organization\":\"SCAV - Canton de Vaud\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Requ\u00e9rant-Pr\u00e9leveur\"],\"distributorIds\":[\"$D_LAUSANNE\",\"$D_MONTREUX\",\"$D_NYON\",\"$D_VEVEY\",\"$D_YVERDON\"]}"

post "users" "{\"email\":\"r.muller@scav.vd.ch\",\"firstName\":\"Robert\",\"lastName\":\"Muller\",\"organization\":\"SCAV - Canton de Vaud\",\"password\":\"Test1234!\",\"tenantId\":\"$TENANT\",\"roles\":[\"Administrator\"],\"distributorIds\":[\"$D_LAUSANNE\",\"$D_MONTREUX\",\"$D_NYON\",\"$D_VEVEY\",\"$D_YVERDON\"]}"

# =============================================
# 3. ANALYSIS PROFILES
# =============================================
echo ""
echo "=== PROFILS D'ANALYSE ==="

# Bacteriologie (category=0)
R=$(post "analysis-profiles" '{"code":"BACT-ECOLI","name":"Escherichia coli","description":"Recherche et denombrement E. coli - indicateur de contamination fecale","category":0}')
P_ECOLI=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"BACT-ENTERO","name":"Enterocoques intestinaux","description":"Recherche et denombrement des enterocoques - indicateur de contamination fecale","category":0}')
P_ENTERO=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"BACT-GAM","name":"Germes aerobies mesophiles","description":"Denombrement des germes aerobies mesophiles a 30C","category":0}')
P_GAM=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"BACT-COLIF","name":"Coliformes totaux","description":"Recherche et denombrement des coliformes totaux","category":0}')
P_COLIF=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"BACT-PSEUDO","name":"Pseudomonas aeruginosa","description":"Recherche Pseudomonas aeruginosa - controle installations sensibles","category":0}')
P_PSEUDO=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"BACT-LEGION","name":"Legionella pneumophila","description":"Recherche et denombrement Legionella - eau chaude sanitaire","category":0}')
P_LEGION=$(getid "$R")

# Chimie (category=1)
R=$(post "analysis-profiles" '{"code":"CHIM-NO3","name":"Nitrates","description":"Dosage des nitrates (NO3) - valeur limite 40 mg/l selon OSEC","category":1}')
P_NO3=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"CHIM-CL2","name":"Chlore residuel","description":"Mesure du chlore residuel libre et total apres traitement","category":1}')
P_CL2=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"CHIM-PEST","name":"Pesticides et metabolites","description":"Screening pesticides et metabolites - valeur limite 0.1 ug/l","category":1}')
P_PEST=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"CHIM-METL","name":"Metaux lourds","description":"Dosage Pb, Cu, Ni, Cr, Cd, As - conformite OSEC","category":1}')
P_METL=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"CHIM-PFAS","name":"PFAS (substances per- et polyfluoroalkylees)","description":"Screening PFAS - contaminants emergents, somme < 0.3 ug/l","category":1}')
P_PFAS=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"CHIM-HAP","name":"Hydrocarbures aromatiques polycycliques","description":"Dosage HAP - surveillance contamination industrielle","category":1}')
P_HAP=$(getid "$R")

# Physique (category=2)
R=$(post "analysis-profiles" '{"code":"PHYS-TURB","name":"Turbidite","description":"Mesure de la turbidite en NTU - valeur limite 1 NTU","category":2}')
P_TURB=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"PHYS-PH","name":"pH et conductivite","description":"Mesure du pH (6.8-8.5) et de la conductivite electrique","category":2}')
P_PH=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"PHYS-TEMP","name":"Temperature de l'\''eau","description":"Mesure de la temperature in situ","category":2}')
P_TEMP=$(getid "$R")

# Autre (category=3)
R=$(post "analysis-profiles" '{"code":"BAIG-COMP","name":"Controle eaux de baignade","description":"Analyse eaux de baignade selon OHyg - E. coli + enterocoques","category":3}')
P_BAIG=$(getid "$R")
R=$(post "analysis-profiles" '{"code":"PISCINE","name":"Controle piscines","description":"Analyse qualite eau de piscine - chlore, pH, bacteriologie","category":3}')
P_PISCINE=$(getid "$R")

# If IDs are empty, fetch existing profiles
if [ -z "$P_ECOLI" ]; then
  echo "  Profils existants, recuperation des IDs..."
  ALL_PROF=$(get "analysis-profiles")
  getpid() { echo "$ALL_PROF" | grep -o "{[^}]*\"code\":\"$1\"[^}]*}" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1; }
  P_ECOLI=$(getpid "BACT-ECOLI"); P_ENTERO=$(getpid "BACT-ENTERO"); P_GAM=$(getpid "BACT-GAM")
  P_COLIF=$(getpid "BACT-COLIF"); P_PSEUDO=$(getpid "BACT-PSEUDO"); P_LEGION=$(getpid "BACT-LEGION")
  P_NO3=$(getpid "CHIM-NO3"); P_CL2=$(getpid "CHIM-CL2"); P_PEST=$(getpid "CHIM-PEST")
  P_METL=$(getpid "CHIM-METL"); P_PFAS=$(getpid "CHIM-PFAS"); P_HAP=$(getpid "CHIM-HAP")
  P_TURB=$(getpid "PHYS-TURB"); P_PH=$(getpid "PHYS-PH"); P_TEMP=$(getpid "PHYS-TEMP")
  P_BAIG=$(getpid "BAIG-COMP"); P_PISCINE=$(getpid "PISCINE")
fi

# =============================================
# 4. ANALYSIS PROGRAMS
# =============================================
echo ""
echo "=== PROGRAMMES D'ANALYSE ==="

R=$(post "analysis-programs" '{"code":"PROG-ROUTINE-VD","name":"Controle de routine eau potable VD","description":"Programme de base Canton de Vaud - parametres microbiologiques et physiques essentiels"}')
PROG1=$(getid "$R")
if [ -n "$PROG1" ]; then
  post "analysis-programs/$PROG1/profiles" "{\"profileIds\":[\"$P_ECOLI\",\"$P_GAM\",\"$P_TURB\",\"$P_PH\"]}"
fi

R=$(post "analysis-programs" '{"code":"PROG-COMPLET-VD","name":"Analyse complete eau potable VD","description":"Programme complet Vaud incluant PFAS et HAP - controle annuel approfondi"}')
PROG2=$(getid "$R")
if [ -n "$PROG2" ]; then
  post "analysis-programs/$PROG2/profiles" "{\"profileIds\":[\"$P_ECOLI\",\"$P_ENTERO\",\"$P_GAM\",\"$P_COLIF\",\"$P_NO3\",\"$P_PEST\",\"$P_METL\",\"$P_PFAS\",\"$P_TURB\",\"$P_PH\",\"$P_TEMP\"]}"
fi

R=$(post "analysis-programs" '{"code":"PROG-LEGIONELLE","name":"Surveillance Legionella","description":"Programme specifique controle Legionella dans les installations d'\''eau chaude sanitaire"}')
PROG3=$(getid "$R")
if [ -n "$PROG3" ]; then
  post "analysis-programs/$PROG3/profiles" "{\"profileIds\":[\"$P_LEGION\",\"$P_TEMP\"]}"
fi

R=$(post "analysis-programs" '{"code":"PROG-EMERGENTS","name":"Contaminants emergents","description":"Programme de surveillance des contaminants emergents - PFAS, HAP, pesticides"}')
PROG4=$(getid "$R")
if [ -n "$PROG4" ]; then
  post "analysis-programs/$PROG4/profiles" "{\"profileIds\":[\"$P_PFAS\",\"$P_HAP\",\"$P_PEST\"]}"
fi

R=$(post "analysis-programs" '{"code":"PROG-BAIGN-VD","name":"Surveillance eaux de baignade VD","description":"Programme Vaud pour les eaux de baignade lac Leman et piscines"}')
PROG5=$(getid "$R")
if [ -n "$PROG5" ]; then
  post "analysis-programs/$PROG5/profiles" "{\"profileIds\":[\"$P_BAIG\",\"$P_ECOLI\",\"$P_ENTERO\",\"$P_PSEUDO\",\"$P_TEMP\",\"$P_PH\"]}"
fi

R=$(post "analysis-programs" '{"code":"PROG-PISCINE-VD","name":"Controle piscines et spas VD","description":"Programme Vaud pour le controle sanitaire des piscines publiques et spas"}')
PROG6=$(getid "$R")
if [ -n "$PROG6" ]; then
  post "analysis-programs/$PROG6/profiles" "{\"profileIds\":[\"$P_PISCINE\",\"$P_ECOLI\",\"$P_PSEUDO\",\"$P_LEGION\",\"$P_CL2\",\"$P_PH\"]}"
fi

# =============================================
# 5. SAMPLING LOCATIONS
# =============================================
echo ""
echo "=== LIEUX DE PRELEVEMENT ==="

# Lausanne
post "sampling-locations" "{\"name\":\"Station de pompage de Saint-Sulpice\",\"locationCode\":\"LS-STP-001\",\"latitude\":46.508,\"longitude\":6.557,\"description\":\"Captage principal eau du Leman - debit 650 l/s\",\"distributorId\":\"$D_LAUSANNE\"}"
post "sampling-locations" "{\"name\":\"Reservoir de Sauvabelin\",\"locationCode\":\"LS-RES-001\",\"latitude\":46.534,\"longitude\":6.634,\"description\":\"Reservoir principal desserte haute-ville - capacite 12000 m3\",\"distributorId\":\"$D_LAUSANNE\"}"
post "sampling-locations" "{\"name\":\"CHUV - Arrivee eau\",\"locationCode\":\"LS-BAT-001\",\"latitude\":46.5255,\"longitude\":6.6424,\"description\":\"Point de controle batiment sensible - Centre hospitalier universitaire\",\"distributorId\":\"$D_LAUSANNE\"}"
post "sampling-locations" "{\"name\":\"Fontaine de la Palud\",\"locationCode\":\"LS-FON-001\",\"latitude\":46.522,\"longitude\":6.633,\"description\":\"Point de controle reseau distribution vieille-ville\",\"distributorId\":\"$D_LAUSANNE\"}"
post "sampling-locations" "{\"name\":\"Ecole de Montriond\",\"locationCode\":\"LS-BAT-002\",\"latitude\":46.5195,\"longitude\":6.627,\"description\":\"Point de controle batiment public - ecole primaire Montriond\",\"distributorId\":\"$D_LAUSANNE\"}"

# Montreux
post "sampling-locations" "{\"name\":\"Source du Pont de Brent\",\"locationCode\":\"MX-SRC-001\",\"latitude\":46.459,\"longitude\":6.895,\"description\":\"Captage source karstique - debit moyen 200 l/min\",\"distributorId\":\"$D_MONTREUX\"}"
post "sampling-locations" "{\"name\":\"Reservoir des Planches\",\"locationCode\":\"MX-RES-001\",\"latitude\":46.434,\"longitude\":6.912,\"description\":\"Reservoir distribution centre Montreux - 3000 m3\",\"distributorId\":\"$D_MONTREUX\"}"
post "sampling-locations" "{\"name\":\"Casino de Montreux - Robinet\",\"locationCode\":\"MX-BAT-001\",\"latitude\":46.433,\"longitude\":6.9085,\"description\":\"Point de controle batiment public - Centre des congres\",\"distributorId\":\"$D_MONTREUX\"}"
post "sampling-locations" "{\"name\":\"Plage de la Maladaire\",\"locationCode\":\"MX-BAIG-001\",\"latitude\":46.448,\"longitude\":6.873,\"description\":\"Zone de baignade officielle Leman - plage publique Montreux\",\"distributorId\":\"$D_MONTREUX\"}"

# Nyon
post "sampling-locations" "{\"name\":\"Station de traitement de Bois-Bougy\",\"locationCode\":\"NY-STT-001\",\"latitude\":46.387,\"longitude\":6.221,\"description\":\"Station de traitement eau du Leman - ultrafiltration membranaire\",\"distributorId\":\"$D_NYON\"}"
post "sampling-locations" "{\"name\":\"Reservoir de Signy\",\"locationCode\":\"NY-RES-001\",\"latitude\":46.394,\"longitude\":6.234,\"description\":\"Reservoir intercommunal desservant Nyon et Prangins\",\"distributorId\":\"$D_NYON\"}"
post "sampling-locations" "{\"name\":\"College de Nyon-Marens\",\"locationCode\":\"NY-BAT-001\",\"latitude\":46.383,\"longitude\":6.238,\"description\":\"Point de controle batiment scolaire\",\"distributorId\":\"$D_NYON\"}"

# Vevey
post "sampling-locations" "{\"name\":\"Captage de la Veveyse\",\"locationCode\":\"VV-SRC-001\",\"latitude\":46.461,\"longitude\":6.841,\"description\":\"Captage riviere Veveyse avec traitement multi-barrieres\",\"distributorId\":\"$D_VEVEY\"}"
post "sampling-locations" "{\"name\":\"Musee Alimentarium - Robinet\",\"locationCode\":\"VV-BAT-001\",\"latitude\":46.46,\"longitude\":6.844,\"description\":\"Point de controle reseau centre-ville Vevey\",\"distributorId\":\"$D_VEVEY\"}"
post "sampling-locations" "{\"name\":\"Plage de Vevey\",\"locationCode\":\"VV-BAIG-001\",\"latitude\":46.459,\"longitude\":6.85,\"description\":\"Zone de baignade officielle - plage principale de Vevey\",\"distributorId\":\"$D_VEVEY\"}"

# Yverdon
post "sampling-locations" "{\"name\":\"Station de pompage des Quatre-Maisons\",\"locationCode\":\"YV-STP-001\",\"latitude\":46.777,\"longitude\":6.641,\"description\":\"Captage nappe alluviale - debit 400 l/s\",\"distributorId\":\"$D_YVERDON\"}"
post "sampling-locations" "{\"name\":\"Reservoir du Mujon\",\"locationCode\":\"YV-RES-001\",\"latitude\":46.781,\"longitude\":6.654,\"description\":\"Reservoir principal Yverdon - capacite 6000 m3\",\"distributorId\":\"$D_YVERDON\"}"
post "sampling-locations" "{\"name\":\"Centre thermal d'Yverdon\",\"locationCode\":\"YV-BAT-001\",\"latitude\":46.778,\"longitude\":6.638,\"description\":\"Point de controle - centre thermal et bains publics\",\"distributorId\":\"$D_YVERDON\"}"
post "sampling-locations" "{\"name\":\"Plage d'Yverdon\",\"locationCode\":\"YV-BAIG-001\",\"latitude\":46.772,\"longitude\":6.629,\"description\":\"Zone de baignade lac de Neuchatel - plage municipale\",\"distributorId\":\"$D_YVERDON\"}"

# =============================================
# 6. DELEGATION (Montreux -> Vevey)
# =============================================
echo ""
echo "=== DELEGATION ==="

post "delegations" "{\"delegatingDistributorId\":\"$D_MONTREUX\",\"delegatedToDistributorId\":\"$D_VEVEY\",\"validFrom\":\"2026-01-01T00:00:00Z\",\"validTo\":null}"

# =============================================
# SUMMARY
# =============================================
echo ""
echo "=================================================="
echo "SEED DATA COMPLETE (Synology - Canton de Vaud)"
echo "=================================================="
echo "Distributeurs: 5"
echo "Utilisateurs: 9 (+1 admin existant)"
echo "Profils d'analyse: 17"
echo "Programmes d'analyse: 6"
echo "Lieux de prelevement: 19"
echo "Delegations: 1 (Montreux -> Vevey)"
echo ""
echo "Comptes (mot de passe: Test1234!):"
echo "  f.blanc@lausanne.ch    - Requerant Lausanne"
echo "  m.rochat@montreux.ch   - Requerant Montreux"
echo "  i.martin@nyon.ch       - Requerant Nyon"
echo "  d.bonvin@vevey.ch      - Requerant Vevey"
echo "  c.dupont@yverdon.ch    - Requerant Yverdon"
echo "  p.favre@scav.vd.ch     - Preleveur (Lsne+Mx+Vvy)"
echo "  v.rouge@scav.vd.ch     - Preleveur (Nyon+Yverdon)"
echo "  a.jaccard@scav.vd.ch   - Req-Preleveur (tous)"
echo "  r.muller@scav.vd.ch    - Admin SCAV"
echo "  admin@aquaplan.ch       - Admin (Admin123!)"
