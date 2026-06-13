#!/usr/bin/env python3
"""Seed realistic data for AquaPlan Synology instance — Canton de Fribourg context.

Distributors: Eausud, SINEF, Commune de Romont, Commune de Chatel-St-Denis
(Different from localhost which uses Fribourg-ville, Bulle, AIEB, Morat)
"""

import json
import requests

API = "http://192.168.1.159:8880/api"

# Login
resp = requests.post(f"{API}/auth/login", json={"email": "admin@aquaplan.ch", "password": "Admin123!"})
token = resp.json()["accessToken"]
headers = {"Authorization": f"Bearer {token}", "Content-Type": "application/json"}


def api_post(path, data):
    r = requests.post(f"{API}/{path}", json=data, headers=headers)
    if r.status_code in (200, 201):
        print(f"  OK: {path}")
        return r.json()
    else:
        print(f"  FAIL ({r.status_code}): {path} -> {r.text[:200]}")
        return None


def api_get(path):
    r = requests.get(f"{API}/{path}", headers=headers)
    return r.json() if r.status_code == 200 else None


# =============================================
# 1. DISTRIBUTORS (Canton de Fribourg — differents de localhost)
# =============================================
print("\n=== DISTRIBUTEURS (Canton de Fribourg) ===")

distributors = {}

dist_data = [
    {
        "name": "Eausud SA",
        "cantonRegion": "Glane / Veveyse",
        "distributionNetwork": "Reseau intercommunal sud fribourgeois - 35000 habitants - dessert 25 communes",
    },
    {
        "name": "SINEF (Syndicat intercommunal des eaux du Nord fribourgeois)",
        "cantonRegion": "Broye / Lac",
        "distributionNetwork": "Reseau intercommunal nord-est Fribourg - 28000 habitants - 18 communes",
    },
    {
        "name": "Commune de Romont - Service des eaux",
        "cantonRegion": "Glane",
        "distributionNetwork": "Reseau communal Romont et environs - 5200 habitants",
    },
    {
        "name": "Commune de Chatel-St-Denis - Service des eaux",
        "cantonRegion": "Veveyse",
        "distributionNetwork": "Reseau communal Chatel-St-Denis - 7100 habitants",
    },
]

for d in dist_data:
    result = api_post("distributors", d)
    if result:
        distributors[d["name"]] = result["id"]

D_EAUSUD = distributors.get("Eausud SA")
D_SINEF = distributors.get("SINEF (Syndicat intercommunal des eaux du Nord fribourgeois)")
D_ROMONT = distributors.get("Commune de Romont - Service des eaux")
D_CHATEL = distributors.get("Commune de Chatel-St-Denis - Service des eaux")

print(f"  IDs: Eausud={D_EAUSUD}, SINEF={D_SINEF}, Romont={D_ROMONT}, Chatel={D_CHATEL}")

# =============================================
# 2. USERS
# =============================================
print("\n=== UTILISATEURS ===")

TENANT_ID = "00000000-0000-0000-0000-000000000001"

users_data = [
    # Mandataires (Requerant) - un par distributeur
    {
        "email": "j.python@eausud.ch",
        "firstName": "Jacques",
        "lastName": "Python",
        "organization": "Eausud SA",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant"],
        "distributorIds": [D_EAUSUD],
    },
    {
        "email": "s.nussbaumer@sinef.ch",
        "firstName": "Sandra",
        "lastName": "Nussbaumer",
        "organization": "SINEF",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant"],
        "distributorIds": [D_SINEF],
    },
    {
        "email": "p.duc@romont.ch",
        "firstName": "Pierre",
        "lastName": "Duc",
        "organization": "Commune de Romont",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant"],
        "distributorIds": [D_ROMONT],
    },
    {
        "email": "m.andrey@chatel.ch",
        "firstName": "Marie",
        "lastName": "Andrey",
        "organization": "Commune de Chatel-St-Denis",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant"],
        "distributorIds": [D_CHATEL],
    },
    # Preleveurs SAAV
    {
        "email": "l.berset@fr.ch",
        "firstName": "Laurent",
        "lastName": "Berset",
        "organization": "SAAV - Canton de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Pr\u00e9leveur"],
        "distributorIds": [D_EAUSUD, D_ROMONT, D_CHATEL],
    },
    {
        "email": "n.hayoz@fr.ch",
        "firstName": "Nadine",
        "lastName": "Hayoz",
        "organization": "SAAV - Canton de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Pr\u00e9leveur"],
        "distributorIds": [D_SINEF],
    },
    # Requerant-Preleveur (double role)
    {
        "email": "c.boschung@fr.ch",
        "firstName": "Claude",
        "lastName": "Boschung",
        "organization": "SAAV - Canton de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant-Pr\u00e9leveur"],
        "distributorIds": [D_EAUSUD, D_SINEF, D_ROMONT, D_CHATEL],
    },
    # Admin SAAV
    {
        "email": "a.brulhart@fr.ch",
        "firstName": "Anne",
        "lastName": "Brulhart",
        "organization": "SAAV - Canton de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Administrator"],
        "distributorIds": [D_EAUSUD, D_SINEF, D_ROMONT, D_CHATEL],
    },
]

for u in users_data:
    api_post("users", u)

# =============================================
# 3. ANALYSIS PROFILES
# =============================================
print("\n=== PROFILS D'ANALYSE ===")

profiles_data = [
    # Bacteriologie (category=0)
    {"code": "BACT-ECOLI", "name": "Escherichia coli", "description": "Recherche et denombrement E. coli - indicateur de contamination fecale", "category": 0},
    {"code": "BACT-ENTERO", "name": "Enterocoques intestinaux", "description": "Recherche et denombrement des enterocoques - indicateur de contamination fecale", "category": 0},
    {"code": "BACT-GAM", "name": "Germes aerobies mesophiles", "description": "Denombrement des germes aerobies mesophiles a 30C", "category": 0},
    {"code": "BACT-COLIF", "name": "Coliformes totaux", "description": "Recherche et denombrement des coliformes totaux", "category": 0},
    {"code": "BACT-PSEUDO", "name": "Pseudomonas aeruginosa", "description": "Recherche Pseudomonas aeruginosa - controle installations sensibles", "category": 0},
    # Chimie (category=1)
    {"code": "CHIM-NO3", "name": "Nitrates", "description": "Dosage des nitrates (NO3) - valeur limite 40 mg/l selon OSEC", "category": 1},
    {"code": "CHIM-CL2", "name": "Chlore residuel", "description": "Mesure du chlore residuel libre et total apres traitement", "category": 1},
    {"code": "CHIM-PEST", "name": "Pesticides et metabolites", "description": "Screening pesticides et metabolites - valeur limite 0.1 ug/l", "category": 1},
    {"code": "CHIM-METL", "name": "Metaux lourds", "description": "Dosage Pb, Cu, Ni, Cr, Cd, As - conformite OSEC", "category": 1},
    {"code": "CHIM-PFAS", "name": "PFAS (substances per- et polyfluoroalkylees)", "description": "Screening PFAS - contaminants emergents, somme < 0.3 ug/l", "category": 1},
    # Physique (category=2)
    {"code": "PHYS-TURB", "name": "Turbidite", "description": "Mesure de la turbidite en NTU - valeur limite 1 NTU", "category": 2},
    {"code": "PHYS-PH", "name": "pH et conductivite", "description": "Mesure du pH (6.8-8.5) et de la conductivite electrique", "category": 2},
    {"code": "PHYS-TEMP", "name": "Temperature de l'eau", "description": "Mesure de la temperature in situ", "category": 2},
    # Autre (category=3)
    {"code": "BAIG-COMP", "name": "Controle eaux de baignade", "description": "Analyse eaux de baignade selon OHyg - E. coli + enterocoques", "category": 3},
]

profile_ids = {}
for p in profiles_data:
    result = api_post("analysis-profiles", p)
    if result:
        profile_ids[p["code"]] = result["id"]

# =============================================
# 4. ANALYSIS PROGRAMS
# =============================================
print("\n=== PROGRAMMES D'ANALYSE ===")

programs_data = [
    {
        "code": "PROG-ROUTINE-FR",
        "name": "Controle de routine eau potable FR",
        "description": "Programme de base Canton de Fribourg - parametres microbiologiques et physiques essentiels",
        "profiles": ["BACT-ECOLI", "BACT-GAM", "PHYS-TURB", "PHYS-PH"],
    },
    {
        "code": "PROG-COMPLET-FR",
        "name": "Analyse complete eau potable FR",
        "description": "Programme complet Fribourg incluant chimie et PFAS - controle annuel approfondi",
        "profiles": ["BACT-ECOLI", "BACT-ENTERO", "BACT-GAM", "BACT-COLIF", "CHIM-NO3", "CHIM-PEST", "CHIM-METL", "CHIM-PFAS", "PHYS-TURB", "PHYS-PH", "PHYS-TEMP"],
    },
    {
        "code": "PROG-BACTERIO-FR",
        "name": "Controle bacteriologique renforce",
        "description": "Programme bacteriologique etendu - utilise en cas de suspicion de contamination",
        "profiles": ["BACT-ECOLI", "BACT-ENTERO", "BACT-GAM", "BACT-COLIF", "BACT-PSEUDO"],
    },
    {
        "code": "PROG-CHIMIE-FR",
        "name": "Surveillance chimique",
        "description": "Programme de surveillance chimique - nitrates, pesticides, metaux, PFAS",
        "profiles": ["CHIM-NO3", "CHIM-PEST", "CHIM-METL", "CHIM-PFAS", "CHIM-CL2"],
    },
    {
        "code": "PROG-BAIGN-FR",
        "name": "Surveillance eaux de baignade FR",
        "description": "Programme Fribourg pour les eaux de baignade - lacs et piscines publiques",
        "profiles": ["BAIG-COMP", "BACT-ECOLI", "BACT-ENTERO", "PHYS-TEMP", "PHYS-PH"],
    },
]

for prog in programs_data:
    profiles_to_add = prog.pop("profiles")
    result = api_post("analysis-programs", prog)
    if result:
        prog_id = result["id"]
        pids = [profile_ids[pc] for pc in profiles_to_add if pc in profile_ids]
        if pids:
            api_post(f"analysis-programs/{prog_id}/profiles", {"profileIds": pids})

# =============================================
# 5. SAMPLING LOCATIONS
# =============================================
print("\n=== LIEUX DE PRELEVEMENT ===")

locations_data = [
    # Eausud SA
    {"name": "Station de traitement de Remaufens", "locationCode": "ES-STT-001", "latitude": 46.5290, "longitude": 6.9010, "description": "Station de traitement principale Eausud - capacite 8000 m3/jour", "distributorId": D_EAUSUD},
    {"name": "Reservoir de Vuisternens-dt-Romont", "locationCode": "ES-RES-001", "latitude": 46.6230, "longitude": 6.9050, "description": "Reservoir intercommunal Eausud secteur Glane - 2500 m3", "distributorId": D_EAUSUD},
    {"name": "Reservoir de La Rougeve (Semsales)", "locationCode": "ES-RES-002", "latitude": 46.5640, "longitude": 6.9290, "description": "Reservoir desserte secteur Veveyse - 1800 m3", "distributorId": D_EAUSUD},
    {"name": "Source de la Broye (Semsales)", "locationCode": "ES-SRC-001", "latitude": 46.5690, "longitude": 6.9380, "description": "Captage source karstique haute Broye - debit 120 l/min", "distributorId": D_EAUSUD},
    {"name": "Ecole de Rue - Robinet cuisine", "locationCode": "ES-BAT-001", "latitude": 46.6170, "longitude": 6.8410, "description": "Point de controle batiment public - ecole primaire de Rue", "distributorId": D_EAUSUD},

    # SINEF
    {"name": "Station de pompage de Courgevaux", "locationCode": "SN-STP-001", "latitude": 46.9050, "longitude": 7.1020, "description": "Captage nappe alluviale lac de Morat - debit 300 l/s", "distributorId": D_SINEF},
    {"name": "Reservoir de Courtepin", "locationCode": "SN-RES-001", "latitude": 46.8780, "longitude": 7.1230, "description": "Reservoir principal SINEF secteur lac - 4000 m3", "distributorId": D_SINEF},
    {"name": "Reservoir de Cressier-sur-Morat", "locationCode": "SN-RES-002", "latitude": 46.9270, "longitude": 7.1340, "description": "Reservoir desserte Cressier et environs - 1200 m3", "distributorId": D_SINEF},
    {"name": "Home medicalis Jeuss - Robinet", "locationCode": "SN-BAT-001", "latitude": 46.9310, "longitude": 7.1070, "description": "Point de controle batiment sensible - EMS communal", "distributorId": D_SINEF},
    {"name": "Plage de Salavaux", "locationCode": "SN-BAIG-001", "latitude": 46.9320, "longitude": 7.0640, "description": "Zone de baignade officielle lac de Morat - plage publique", "distributorId": D_SINEF},

    # Romont
    {"name": "Source du Glaney", "locationCode": "RM-SRC-001", "latitude": 46.6930, "longitude": 6.9180, "description": "Captage source principale alimentant Romont - debit moyen 80 l/min", "distributorId": D_ROMONT},
    {"name": "Reservoir de la Condeminaz", "locationCode": "RM-RES-001", "latitude": 46.6960, "longitude": 6.9210, "description": "Reservoir principal ville de Romont - 2000 m3", "distributorId": D_ROMONT},
    {"name": "College de Romont - Robinet", "locationCode": "RM-BAT-001", "latitude": 46.6945, "longitude": 6.9170, "description": "Point de controle batiment scolaire - college secondaire", "distributorId": D_ROMONT},
    {"name": "Fontaine medievale Grand-Rue", "locationCode": "RM-FON-001", "latitude": 46.6940, "longitude": 6.9160, "description": "Point de controle reseau distribution centre historique", "distributorId": D_ROMONT},

    # Chatel-St-Denis
    {"name": "Source des Paccots", "locationCode": "CH-SRC-001", "latitude": 46.5070, "longitude": 6.8950, "description": "Captage source montagne - secteur Les Paccots - debit 60 l/min", "distributorId": D_CHATEL},
    {"name": "Reservoir de Fruence", "locationCode": "CH-RES-001", "latitude": 46.5260, "longitude": 6.8990, "description": "Reservoir principal Chatel-St-Denis - 1500 m3", "distributorId": D_CHATEL},
    {"name": "Ecole primaire Chatel - Robinet", "locationCode": "CH-BAT-001", "latitude": 46.5250, "longitude": 6.9000, "description": "Point de controle batiment scolaire", "distributorId": D_CHATEL},
    {"name": "Piscine des Joncs", "locationCode": "CH-BAIG-001", "latitude": 46.5240, "longitude": 6.8980, "description": "Piscine publique de Chatel-St-Denis - controle sanitaire", "distributorId": D_CHATEL},
]

for loc in locations_data:
    api_post("sampling-locations", loc)

# =============================================
# 6. Summary
# =============================================
print("\n" + "=" * 50)
print("SEED DATA COMPLETE (Synology - Canton de Fribourg)")
print("=" * 50)
print(f"Distributeurs: {len(dist_data)}")
print(f"Utilisateurs: {len(users_data)} (+1 admin existant)")
print(f"Profils d'analyse: {len(profiles_data)}")
print(f"Programmes d'analyse: {len(programs_data)}")
print(f"Lieux de prelevement: {len(locations_data)}")
print("\nComptes utilisateurs (mot de passe: Test1234!):")
print("  Mandataires (Requerant):")
print("    j.python@eausud.ch       - Eausud SA")
print("    s.nussbaumer@sinef.ch    - SINEF")
print("    p.duc@romont.ch          - Commune de Romont")
print("    m.andrey@chatel.ch       - Commune de Chatel-St-Denis")
print("  Preleveurs SAAV:")
print("    l.berset@fr.ch           - Eausud + Romont + Chatel")
print("    n.hayoz@fr.ch            - SINEF")
print("  Requerant-Preleveur:")
print("    c.boschung@fr.ch         - Tous distributeurs")
print("  Administrateurs:")
print("    admin@aquaplan.ch (Admin123!)")
print("    a.brulhart@fr.ch (Test1234!)")
