#!/usr/bin/env python3
"""Seed realistic data for AquaPlan - Canton de Fribourg SAAV context."""

import json
import os

import requests

# Surchargeable pour lancer le script depuis un conteneur (voir le service
# aquaplan-seed de docker-compose.demo.yml), ou contre une autre instance.
API = os.environ.get("AQUAPLAN_API_URL", "http://localhost:5002/api").rstrip("/")

# Login. Les identifiants sont surchargeables : en Production (Render, NAS) le
# compte admin@aquaplan.ch n'est pas seede, il est cree a partir de
# INITIAL_ADMIN_EMAIL / INITIAL_ADMIN_PASSWORD au premier demarrage de l'API.
ADMIN_EMAIL = os.environ.get("AQUAPLAN_ADMIN_EMAIL", "admin@aquaplan.ch")
ADMIN_PASSWORD = os.environ.get("AQUAPLAN_ADMIN_PASSWORD", "Admin123!")

resp = requests.post(f"{API}/auth/login", json={"email": ADMIN_EMAIL, "password": ADMIN_PASSWORD})
if resp.status_code != 200:
    raise SystemExit(
        f"Echec de connexion sur {API}/auth/login ({resp.status_code}).\n"
        f"Compte utilise : {ADMIN_EMAIL}. Verifier AQUAPLAN_API_URL, "
        f"AQUAPLAN_ADMIN_EMAIL et AQUAPLAN_ADMIN_PASSWORD."
    )
token = resp.json()["accessToken"]
headers = {"Authorization": f"Bearer {token}", "Content-Type": "application/json"}


# Le resume final comptait les entrees *tentees*, pas celles reellement creees :
# un seed entierement en echec s'affichait comme un succes. On compte les vraies.
created = {}
failed = {}


def api_post(path, data):
    r = requests.post(f"{API}/{path}", json=data, headers=headers)
    if r.status_code in (200, 201):
        print(f"  OK: {path}")
        created[path] = created.get(path, 0) + 1
        return r.json()
    else:
        print(f"  FAIL ({r.status_code}): {path} -> {r.text[:200]}")
        failed[path] = failed.get(path, 0) + 1
        return None


def api_get(path):
    r = requests.get(f"{API}/{path}", headers=headers)
    return r.json() if r.status_code == 200 else None


# =============================================
# 1. DISTRIBUTORS (distributeurs d'eau potable)
# =============================================
print("\n=== DISTRIBUTEURS ===")

# Check if distributors already exist
existing = api_get("distributors")
if existing and len(existing) > 0:
    print(f"  {len(existing)} distributeurs existent deja")
    distributors = {d["name"]: d["id"] for d in existing}
else:
    distributors = {}

dist_data = [
    {"name": "Service des eaux de la Ville de Fribourg", "cantonRegion": "Sarine", "distributionNetwork": "Reseau urbain Fribourg - 40000 habitants"},
    {"name": "Commune de Bulle - Service des eaux", "cantonRegion": "Gruyere", "distributionNetwork": "Reseau communal Bulle - 25000 habitants"},
    {"name": "Association intercommunale des eaux de la Broye (AIEB)", "cantonRegion": "Broye", "distributionNetwork": "Reseau intercommunal Broye - 15000 habitants"},
    {"name": "Commune de Morat - Service des eaux", "cantonRegion": "Lac", "distributionNetwork": "Reseau communal Morat/Murten - 8500 habitants"},
]

for d in dist_data:
    if d["name"] not in distributors:
        result = api_post("distributors", d)
        if result:
            distributors[d["name"]] = result["id"]
    else:
        print(f"  SKIP: {d['name']} (existe)")

D_FRIBOURG = distributors.get("Service des eaux de la Ville de Fribourg")
D_BULLE = distributors.get("Commune de Bulle - Service des eaux")
D_BROYE = distributors.get("Association intercommunale des eaux de la Broye (AIEB)")
D_MORAT = distributors.get("Commune de Morat - Service des eaux")

print(f"  IDs: Fribourg={D_FRIBOURG}, Bulle={D_BULLE}, Broye={D_BROYE}, Morat={D_MORAT}")

# =============================================
# 1b. SECTORS (secteurs de prelevement)
# =============================================
# Un lieu de prelevement exige un SectorId valide (FK NOT NULL). Sans cette
# etape, POST /api/sampling-locations echoue en 500 (violation de FK).
print("\n=== SECTEURS ===")

sectors_data = [
    {"name": "Fribourg - Centre", "code": "SEC-FR-C", "description": "Secteur centre-ville et Bourg", "distributorId": D_FRIBOURG},
    {"name": "Fribourg - Peripherie", "code": "SEC-FR-P", "description": "Schoenberg, Jura, Torry", "distributorId": D_FRIBOURG},
    {"name": "Bulle - Ville", "code": "SEC-BU-V", "description": "Bulle centre et La Tour-de-Treme", "distributorId": D_BULLE},
    {"name": "Bulle - Riaz", "code": "SEC-BU-R", "description": "Riaz et batiments sensibles", "distributorId": D_BULLE},
    {"name": "Broye - Estavayer", "code": "SEC-BR-E", "description": "Estavayer-le-Lac et rive sud", "distributorId": D_BROYE},
    {"name": "Broye - Domdidier", "code": "SEC-BR-D", "description": "Domdidier, Cugy et environs", "distributorId": D_BROYE},
    {"name": "Morat - Ville", "code": "SEC-MO-V", "description": "Morat/Murten et zone de baignade", "distributorId": D_MORAT},
    {"name": "Morat - Mont-Vully", "code": "SEC-MO-M", "description": "Mont-Vully et hauts du lac", "distributorId": D_MORAT},
]

existing_sectors = api_get("sectors") or []
sector_ids = {sec["code"]: sec["id"] for sec in existing_sectors}

for sec in sectors_data:
    if sec["code"] in sector_ids:
        print(f"  SKIP: {sec['code']} (existe)")
    else:
        result = api_post("sectors", sec)
        if result:
            sector_ids[sec["code"]] = result["id"]

# =============================================
# 2. USERS (utilisateurs par role)
# =============================================
print("\n=== UTILISATEURS ===")

TENANT_ID = "00000000-0000-0000-0000-000000000001"

users_data = [
    # Mandataires (Requerant) - un par distributeur
    {
        "email": "m.schneider@fribourg.ch",
        "firstName": "Marc", "lastName": "Schneider",
        "organization": "Ville de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant"],
        "distributorIds": [D_FRIBOURG],
    },
    {
        "email": "c.pythoud@bulle.ch",
        "firstName": "Christophe", "lastName": "Pythoud",
        "organization": "Commune de Bulle",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant"],
        "distributorIds": [D_BULLE],
    },
    {
        "email": "s.aeby@aieb.ch",
        "firstName": "Sophie", "lastName": "Aeby",
        "organization": "AIEB",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant"],
        "distributorIds": [D_BROYE],
    },
    {
        "email": "p.zbinden@morat.ch",
        "firstName": "Peter", "lastName": "Zbinden",
        "organization": "Commune de Morat",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant"],
        "distributorIds": [D_MORAT],
    },
    # Preleveurs SAAV
    {
        "email": "j.ducrest@saav.fr.ch",
        "firstName": "Jean-Pierre", "lastName": "Ducrest",
        "organization": "SAAV - Canton de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Pr\u00e9leveur"],
        "distributorIds": [D_FRIBOURG, D_BULLE],
    },
    {
        "email": "n.waeber@saav.fr.ch",
        "firstName": "Nathalie", "lastName": "Waeber",
        "organization": "SAAV - Canton de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Pr\u00e9leveur"],
        "distributorIds": [D_BROYE, D_MORAT],
    },
    # Requerant-Preleveur (double role)
    {
        "email": "l.brulhart@saav.fr.ch",
        "firstName": "Laurent", "lastName": "Brulhart",
        "organization": "SAAV - Canton de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Requ\u00e9rant-Pr\u00e9leveur"],
        "distributorIds": [D_FRIBOURG, D_BULLE, D_BROYE, D_MORAT],
    },
    # Admin SAAV supplementaire
    {
        "email": "a.murith@saav.fr.ch",
        "firstName": "Anne", "lastName": "Murith",
        "organization": "SAAV - Canton de Fribourg",
        "password": "Test1234!",
        "tenantId": TENANT_ID,
        "roles": ["Administrator"],
        "distributorIds": [D_FRIBOURG, D_BULLE, D_BROYE, D_MORAT],
    },
]

existing_users = api_get("users")
existing_emails = {u["email"] for u in existing_users} if existing_users else set()
user_ids = {}

for u in users_data:
    if u["email"] in existing_emails:
        print(f"  SKIP: {u['email']} (existe)")
        for eu in existing_users:
            if eu["email"] == u["email"]:
                user_ids[u["email"]] = eu["id"]
    else:
        result = api_post("users", u)
        if result:
            user_ids[u["email"]] = result["id"]

# =============================================
# 3. ANALYSIS PROFILES (profils d'analyse)
# =============================================
print("\n=== PROFILS D'ANALYSE ===")

profiles_data = [
    # Bacteriologie (category=0)
    {"code": "BACT-ECOLI", "name": "Escherichia coli", "description": "Recherche et denombrement E. coli - indicateur de contamination fecale", "category": 0},
    {"code": "BACT-ENTERO", "name": "Enterocoques intestinaux", "description": "Recherche et denombrement des enterocoques - indicateur de contamination fecale", "category": 0},
    {"code": "BACT-GAM", "name": "Germes aerobies mesophiles", "description": "Denombrement des germes aerobies mesophiles a 30C - indicateur de qualite microbiologique generale", "category": 0},
    {"code": "BACT-COLIF", "name": "Coliformes totaux", "description": "Recherche et denombrement des coliformes totaux", "category": 0},
    # Chimie (category=1)
    {"code": "CHIM-NO3", "name": "Nitrates", "description": "Dosage des nitrates (NO3) - valeur limite 40 mg/l selon OSEC", "category": 1},
    {"code": "CHIM-CL2", "name": "Chlore residuel", "description": "Mesure du chlore residuel libre et total apres traitement", "category": 1},
    {"code": "CHIM-PEST", "name": "Pesticides et metabolites", "description": "Screening pesticides et metabolites pertinents - valeur limite 0.1 ug/l par substance", "category": 1},
    {"code": "CHIM-METL", "name": "Metaux lourds", "description": "Dosage Pb, Cu, Ni, Cr, Cd, As - controle conformite OSEC", "category": 1},
    # Physique (category=2)
    {"code": "PHYS-TURB", "name": "Turbidite", "description": "Mesure de la turbidite en NTU - valeur limite 1 NTU selon OSEC", "category": 2},
    {"code": "PHYS-PH", "name": "pH et conductivite", "description": "Mesure du pH (6.8-8.5) et de la conductivite electrique", "category": 2},
    {"code": "PHYS-TEMP", "name": "Temperature de l'eau", "description": "Mesure de la temperature in situ au point de prelevement", "category": 2},
    # Autre (category=3)
    {"code": "BAIG-COMP", "name": "Controle eaux de baignade", "description": "Analyse specifique eaux de baignade selon OHyg - E. coli + enterocoques", "category": 3},
]

# Un profil exige un ContainerId valide : on mappe chaque categorie sur le
# contenant seede par AnalysisCatalogSeeder (0=bacterio, 1=chimie, 2=physique, 3=autre).
containers = api_get("containers") or []
container_by_code = {c["code"]: c["id"] for c in containers}
CONTAINER_BY_CATEGORY = {
    0: container_by_code.get("BACT-V250"),
    1: container_by_code.get("CHEM-PET500"),
    2: container_by_code.get("PHY-V100"),
    3: container_by_code.get("BACT-V250"),
}
CONTAINER_BY_CODE_OVERRIDE = {
    "CHIM-PEST": container_by_code.get("PEST-V1000"),
    "CHIM-METL": container_by_code.get("CHEM-PEHD250"),
}

existing_profiles = api_get("analysis-profiles")
existing_codes = {p["code"] for p in existing_profiles} if existing_profiles else set()
profile_ids = {}

for p in profiles_data:
    if p["code"] in existing_codes:
        print(f"  SKIP: {p['code']} (existe)")
        for ep in existing_profiles:
            if ep["code"] == p["code"]:
                profile_ids[p["code"]] = ep["id"]
    else:
        payload = dict(p)
        payload["containerId"] = (
            CONTAINER_BY_CODE_OVERRIDE.get(p["code"])
            or CONTAINER_BY_CATEGORY.get(p["category"])
        )
        result = api_post("analysis-profiles", payload)
        if result:
            profile_ids[p["code"]] = result["id"]

# =============================================
# 4. ANALYSIS PROGRAMS (programmes d'analyse)
# =============================================
print("\n=== PROGRAMMES D'ANALYSE ===")

programs_data = [
    {
        "code": "PROG-ROUTINE",
        "name": "Controle de routine eau potable",
        "description": "Programme de base pour le controle regulier de l'eau potable - parametres microbiologiques et physiques essentiels",
        "profiles": ["BACT-ECOLI", "BACT-GAM", "PHYS-TURB", "PHYS-PH"],
    },
    {
        "code": "PROG-COMPLET",
        "name": "Analyse complete eau potable",
        "description": "Programme complet incluant chimie, microbiologie et physique - controle annuel approfondi",
        "profiles": ["BACT-ECOLI", "BACT-ENTERO", "BACT-GAM", "BACT-COLIF", "CHIM-NO3", "CHIM-PEST", "CHIM-METL", "PHYS-TURB", "PHYS-PH", "PHYS-TEMP"],
    },
    {
        "code": "PROG-BACT",
        "name": "Controle bacteriologique",
        "description": "Programme microbiologique complet - tous les indicateurs bacteriologiques",
        "profiles": ["BACT-ECOLI", "BACT-ENTERO", "BACT-GAM", "BACT-COLIF"],
    },
    {
        "code": "PROG-BAIGN",
        "name": "Surveillance eaux de baignade",
        "description": "Programme specifique pour les eaux de baignade selon OHyg - lacs et piscines",
        "profiles": ["BAIG-COMP", "BACT-ECOLI", "BACT-ENTERO", "PHYS-TEMP", "PHYS-PH"],
    },
]

existing_programs = api_get("analysis-programs")
existing_prog_codes = {p["code"] for p in existing_programs} if existing_programs else set()

for prog in programs_data:
    profiles_to_add = prog.pop("profiles")
    prog_id = None
    if prog["code"] in existing_prog_codes:
        print(f"  SKIP: {prog['code']} (existe)")
        for ep in existing_programs:
            if ep["code"] == prog["code"]:
                prog_id = ep["id"]
    else:
        result = api_post("analysis-programs", prog)
        if result:
            prog_id = result["id"]

    # Le rattachement des profils doit etre idempotent : un programme cree lors
    # d'un run precedent (ou les profils avaient echoue) restait vide a jamais
    # parce que le rattachement n'existait que sur la branche "creation".
    if prog_id:
        detail = api_get(f"analysis-programs/{prog_id}") or {}
        linked = {lp.get("code") for lp in (detail.get("profiles") or [])}
        pids = [
            profile_ids[pc]
            for pc in profiles_to_add
            if pc in profile_ids and pc not in linked
        ]
        if pids:
            api_post(f"analysis-programs/{prog_id}/profiles", {"profileIds": pids})

# =============================================
# 5. SAMPLING LOCATIONS (LDP)
# =============================================
print("\n=== LIEUX DE PRELEVEMENT ===")

locations_data = [
    # Fribourg (Sarine)
    {"name": "Reservoir de Grandfey", "locationCode": "FR-RES-001", "latitude": 46.8065, "longitude": 7.1472, "description": "Reservoir principal alimentant le centre-ville de Fribourg - capacite 5000 m3", "distributorId": D_FRIBOURG},
    {"name": "Station de pompage Tuffiere", "locationCode": "FR-STP-001", "latitude": 46.7891, "longitude": 7.1580, "description": "Captage source de la Tuffiere - debit moyen 1200 l/min", "distributorId": D_FRIBOURG},
    {"name": "Fontaine Place Python", "locationCode": "FR-FON-001", "latitude": 46.8018, "longitude": 7.1512, "description": "Point de controle reseau distribution centre-ville", "distributorId": D_FRIBOURG},
    {"name": "Ecole du Schoenberg - Robinet cuisine", "locationCode": "FR-BAT-001", "latitude": 46.7950, "longitude": 7.1410, "description": "Point de controle batiment public - ecole primaire", "distributorId": D_FRIBOURG},
    # Bulle (Gruyere)
    {"name": "Source de la Tzintre", "locationCode": "BU-SRC-001", "latitude": 46.6171, "longitude": 7.0595, "description": "Captage principal de Bulle - source karstique, debit variable", "distributorId": D_BULLE},
    {"name": "Reservoir de la Tour-de-Treme", "locationCode": "BU-RES-001", "latitude": 46.6090, "longitude": 7.0720, "description": "Reservoir desserte quartiers sud - capacite 2000 m3", "distributorId": D_BULLE},
    {"name": "Hopital de Riaz - Arrivee eau", "locationCode": "BU-BAT-001", "latitude": 46.6390, "longitude": 7.0490, "description": "Point de controle batiment sensible - Hopital du Sud fribourgeois", "distributorId": D_BULLE},
    {"name": "Place du Marche - Fontaine", "locationCode": "BU-FON-001", "latitude": 46.6190, "longitude": 7.0560, "description": "Point de controle reseau distribution centre Bulle", "distributorId": D_BULLE},
    # Broye
    {"name": "Station de traitement Estavayer", "locationCode": "BR-STT-001", "latitude": 46.8490, "longitude": 6.8440, "description": "Station de traitement principale AIEB - eau du lac de Neuchatel", "distributorId": D_BROYE},
    {"name": "Reservoir de Domdidier", "locationCode": "BR-RES-001", "latitude": 46.8720, "longitude": 6.9640, "description": "Reservoir intercommunal desservant Domdidier et environs", "distributorId": D_BROYE},
    {"name": "Ecole de Cugy", "locationCode": "BR-BAT-001", "latitude": 46.8340, "longitude": 6.9100, "description": "Point de controle batiment public - ecole regionale", "distributorId": D_BROYE},
    # Morat (Lac)
    {"name": "Station de pompage du Lac de Morat", "locationCode": "MO-STP-001", "latitude": 46.9270, "longitude": 7.1050, "description": "Captage eau du lac de Morat avec traitement membranaire", "distributorId": D_MORAT},
    {"name": "Reservoir de Mont-Vully", "locationCode": "MO-RES-001", "latitude": 46.9410, "longitude": 7.0830, "description": "Reservoir altitude desservant Mont-Vully - capacite 800 m3", "distributorId": D_MORAT},
    {"name": "Plage de Morat", "locationCode": "MO-BAIG-001", "latitude": 46.9290, "longitude": 7.1090, "description": "Zone de baignade officielle - plage principale de Morat", "distributorId": D_MORAT},
    {"name": "Maison de retraite Les Pres-d'Orsens", "locationCode": "MO-BAT-001", "latitude": 46.9310, "longitude": 7.1120, "description": "Point de controle batiment sensible - EMS", "distributorId": D_MORAT},
]

# SectorId est requis (FK NOT NULL) : chaque LDP est rattache a son secteur.
LOCATION_SECTOR = {
    "FR-RES-001": "SEC-FR-C", "FR-STP-001": "SEC-FR-P",
    "FR-FON-001": "SEC-FR-C", "FR-BAT-001": "SEC-FR-P",
    "BU-SRC-001": "SEC-BU-V", "BU-RES-001": "SEC-BU-V",
    "BU-BAT-001": "SEC-BU-R", "BU-FON-001": "SEC-BU-V",
    "BR-STT-001": "SEC-BR-E", "BR-RES-001": "SEC-BR-D",
    "BR-BAT-001": "SEC-BR-D",
    "MO-STP-001": "SEC-MO-V", "MO-RES-001": "SEC-MO-M",
    "MO-BAIG-001": "SEC-MO-V", "MO-BAT-001": "SEC-MO-V",
}

existing_locs = api_get("sampling-locations")
existing_loc_codes = set()
if existing_locs:
    # Handle both list and paginated response
    items = existing_locs if isinstance(existing_locs, list) else existing_locs.get("items", [])
    existing_loc_codes = {loc["locationCode"] for loc in items}

for loc in locations_data:
    if loc["locationCode"] in existing_loc_codes:
        print(f"  SKIP: {loc['locationCode']} (existe)")
    else:
        payload = dict(loc)
        payload["sectorId"] = sector_ids.get(LOCATION_SECTOR[loc["locationCode"]])
        api_post("sampling-locations", payload)

# =============================================
# 6. ORDERS & SAMPLING ROUNDS (commandes et tournees)
# =============================================
# Sans commandes ni tournees, les ecrans principaux de l'app sont vides.
print("\n=== COMMANDES & TOURNEES ===")

from datetime import datetime, timedelta, timezone

locs = api_get("sampling-locations") or []
loc_items = locs if isinstance(locs, list) else locs.get("items", [])
loc_by_code = {loc["locationCode"]: loc for loc in loc_items}

progs = api_get("analysis-programs") or []
prog_ids = {pr["code"]: pr["id"] for pr in progs}

usrs = api_get("users") or []
usr_items = usrs if isinstance(usrs, list) else usrs.get("items", [])
user_ids = {u["email"]: u["id"] for u in usr_items}

P_DUCREST = user_ids.get("j.ducrest@saav.fr.ch")
P_WAEBER = user_ids.get("n.waeber@saav.fr.ch")
P_BRULHART = user_ids.get("l.brulhart@saav.fr.ch")

TODAY = datetime.now(timezone.utc).replace(hour=8, minute=0, second=0, microsecond=0)


def iso(days):
    return (TODAY + timedelta(days=days)).isoformat()


# (code LDP, code programme, preleveur, decalage en jours, notes, non planifie)
orders_data = [
    ("FR-RES-001", "PROG-ROUTINE", P_DUCREST, 2, "Controle trimestriel reservoir principal", False),
    ("FR-STP-001", "PROG-COMPLET", P_DUCREST, 2, "Analyse annuelle approfondie du captage", False),
    ("FR-FON-001", "PROG-BACT", P_DUCREST, 3, "Controle reseau centre-ville", False),
    ("FR-BAT-001", "PROG-ROUTINE", P_DUCREST, 3, "Controle annuel ecole primaire", False),
    ("BU-SRC-001", "PROG-COMPLET", P_DUCREST, 5, "Controle source karstique apres fortes pluies", False),
    ("BU-RES-001", "PROG-ROUTINE", P_DUCREST, 5, "Controle trimestriel reservoir sud", False),
    ("BU-BAT-001", "PROG-BACT", P_DUCREST, 6, "Batiment sensible - controle renforce hopital", False),
    ("BR-STT-001", "PROG-COMPLET", P_WAEBER, 8, "Controle station de traitement AIEB", False),
    ("BR-RES-001", "PROG-ROUTINE", P_WAEBER, 8, "Controle trimestriel Domdidier", False),
    ("BR-BAT-001", "PROG-BACT", P_WAEBER, 9, "Controle ecole regionale de Cugy", False),
    ("MO-STP-001", "PROG-COMPLET", P_WAEBER, 12, "Controle captage lac avec traitement membranaire", False),
    ("MO-BAIG-001", "PROG-BAIGN", P_WAEBER, 12, "Surveillance saisonniere zone de baignade", False),
    ("MO-BAT-001", "PROG-BACT", P_BRULHART, 13, "Controle EMS - batiment sensible", False),
    ("FR-FON-001", "PROG-BACT", P_DUCREST, -1, "Suspicion de pollution signalee par un riverain", True),
    ("BU-SRC-001", "PROG-COMPLET", P_DUCREST, 0, "Controle complementaire suite a un depassement", True),
]

existing_orders = api_get("orders") or []
existing_items = existing_orders if isinstance(existing_orders, list) else existing_orders.get("items", [])
order_ids_by_loc = {}

if existing_items:
    print(f"  {len(existing_items)} commandes existent deja - creation ignoree")
else:
    for loc_code, prog_code, preleveur, offset, notes, unplanned in orders_data:
        loc = loc_by_code.get(loc_code)
        if not loc or prog_code not in prog_ids:
            print(f"  SKIP: commande {loc_code} (LDP ou programme manquant)")
            continue
        payload = {
            "distributorId": loc["distributorId"],
            "samplingLocationId": loc["id"],
            "preleveurId": preleveur,
            "plannedDate": iso(offset),
            "analysisProgramIds": [prog_ids[prog_code]],
            "notes": notes,
            "isUnplanned": unplanned,
        }
        if unplanned:
            payload["unplannedReason"] = 0 if "pollution" in notes.lower() else 2
            payload["unplannedReasonDetails"] = notes
        result = api_post("orders", payload)
        if result:
            order_ids_by_loc.setdefault(loc_code, []).append(result["id"])

# --- Tournees : une par distributeur, alimentee avec ses commandes
rounds_data = [
    ("Tournee Sarine - semaine courante", D_FRIBOURG, P_DUCREST, 4,
     ["FR-RES-001", "FR-STP-001", "FR-FON-001", "FR-BAT-001"]),
    ("Tournee Gruyere - Bulle et Riaz", D_BULLE, P_DUCREST, 7,
     ["BU-SRC-001", "BU-RES-001", "BU-BAT-001"]),
    ("Tournee Broye - Estavayer et Domdidier", D_BROYE, P_WAEBER, 10,
     ["BR-STT-001", "BR-RES-001", "BR-BAT-001"]),
    ("Tournee Lac - Morat et Mont-Vully", D_MORAT, P_WAEBER, 14,
     ["MO-STP-001", "MO-BAIG-001", "MO-BAT-001"]),
]

existing_rounds = api_get("sampling-rounds") or []
existing_round_items = existing_rounds if isinstance(existing_rounds, list) else existing_rounds.get("items", [])

if existing_round_items:
    print(f"  {len(existing_round_items)} tournees existent deja - creation ignoree")
else:
    for name, dist_id, preleveur, deadline_offset, loc_codes in rounds_data:
        if not dist_id:
            continue
        rnd = api_post("sampling-rounds", {
            "name": name,
            "description": "Tournee de prelevement planifiee",
            "deadline": iso(deadline_offset),
            "notes": None,
            "distributorId": dist_id,
        })
        if not rnd:
            continue
        rid = rnd["id"]
        for code in loc_codes:
            for oid in order_ids_by_loc.get(code, []):
                api_post(f"sampling-rounds/{rid}/orders", {"orderId": oid})
        if preleveur:
            api_post(f"sampling-rounds/{rid}/assign", {"preleveurId": preleveur})

# =============================================
# 6. Summary
# =============================================
print("\n" + "=" * 50)
print("SEED DATA COMPLETE")
print("=" * 50)
print(f"Distributeurs:        {created.get('distributors', 0)}/{len(dist_data)} crees")
print(f"Secteurs:             {created.get('sectors', 0)}/{len(sectors_data)} crees")
print(f"Utilisateurs:         {created.get('users', 0)}/{len(users_data)} crees (+1 admin existant)")
print(f"Profils d'analyse:    {created.get('analysis-profiles', 0)}/{len(profiles_data)} crees")
print(f"Programmes d'analyse: {created.get('analysis-programs', 0)}/{len(programs_data)} crees")
print(f"Lieux de prelevement: {created.get('sampling-locations', 0)}/{len(locations_data)} crees")
print(f"Commandes:            {created.get('orders', 0)}/{len(orders_data)} creees")
print(f"Tournees:             {created.get('sampling-rounds', 0)}/{len(rounds_data)} creees")
if failed:
    print("\nECHECS (ce seed n'est PAS complet) :")
    for path, n in sorted(failed.items()):
        print(f"  {path}: {n}")
print("\nComptes utilisateurs (mot de passe: Test1234!):")
print("  Mandataires (Requerant):")
print("    m.schneider@fribourg.ch - Ville de Fribourg")
print("    c.pythoud@bulle.ch - Commune de Bulle")
print("    s.aeby@aieb.ch - AIEB (Broye)")
print("    p.zbinden@morat.ch - Commune de Morat")
print("  Preleveurs:")
print("    j.ducrest@saav.fr.ch - Fribourg + Bulle")
print("    n.waeber@saav.fr.ch - Broye + Morat")
print("  Requerant-Preleveur:")
print("    l.brulhart@saav.fr.ch - Tous distributeurs")
print("  Administrateurs:")
print("    admin@aquaplan.ch (Admin123!)")
print("    a.murith@saav.fr.ch (Test1234!)")
