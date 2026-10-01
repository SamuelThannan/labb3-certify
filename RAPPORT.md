# Teknisk leveransrapport

**Uppdrag:** Certify AB — Digitala certifikat med publik verifiering
**Konsultteam:** Samuel Thannan
**Datum:** 01-10-2026
**Version:** 1.0

## Sammanfattning

Vi har byggt ett API för Certify AB som gör det möjligt att skapa digitala certifikat och verifiera dem publikt via en unik länk. Istället för att skicka ett Word-dokument som vem som helst kan ändra kan HR-avdelningar nu generera certifikat via API:et och ge mottagaren en länk som visar om certifikatet är äkta. Lösningen är byggd för att köras i Azure Container Apps och skalas automatiskt vid hög belastning.

## Vad som levereras

| Komponent | Teknisk lösning | Status |
|---|---|---|
| REST API | .NET 8 WebAPI, 5 endpoints |  Levererat |
| Containerisering | Docker, multi-stage build |  Levererat |
| Driftsättning | Azure Container Apps (Bicep-mall) |  Kodat, ej deployat* |
| Bildarkiv | Azure Container Registry |  Kodat, ej deployat* |
| Fillagring | Azure Blob Storage |  Kodat, ej deployat* |
| Infrastruktur som kod | Bicep |  Levererat, syntaktiskt verifierat |
| Automatiserad pipeline | GitHub Actions |  Levererat, build/test grönt |
| API-dokumentation | Swagger UI (/swagger) |  Levererat |

*På grund av våra konton har vi inte tillgång till biceps funktioner.

## Utanför leveransens scope

| Punkt | Motivering |
|---|---|
| Autentisering för slutanvändare | Kräver Entra ID-integration och definierade roller, utanför tidsramen för denna sprint |
| PDF-generering av certifikat | Certifikaten returneras som JSON istället för PDF — enklare att bygga utan externa bibliotek, se kommentar i ARCHITECTURE.md |
| Autoskalning | Container App körs med fast antal repliker (2) istället för automatisk skalning |
| Skarp Azure-deployment | Kontot saknade behörighet på resursgruppen, se Känd begränsning |

## Arkitektur

```
[Klient]
    │
    ▼
[Azure Container Apps — Certify API]
    │
    └──► [Azure Blob Storage — certifikatdata]
                │
                ▼
      [Publik verifiering via /verify/{uuid}]
```

### Motiverade arkitekturval

**Container Apps istället för AKS:** Projektet är litet, ett API med få endpoints, så det kändes onödigt att sätta upp ett helt Kubernetes-kluster. Container Apps sköter skalning och drift automatiskt, vilket passar ett tidigt skede bättre.

**Bicep istället för manuell konfiguration:** Ger en reproducerbar, versionshanterad beskrivning av infrastrukturen istället för klick i portalen som ingen kommer ihåg exakt hur de gjordes.

**Blob Storage för certifikatdata:** Billigt, skalar automatiskt med antal certifikat, och integreras enkelt med Managed Identity utan att vi behöver hantera några nycklar själva.

## Säkerhetsarkitektur

| Resurs | Åtkomstkontroll |
|---|---|
| Azure Container Apps | Managed Identity — ingen hårdkodad nyckel |
| Azure Blob Storage | RBAC via Managed Identity (Storage Blob Data Contributor) |
| Pipeline-credentials | GitHub Secrets — aldrig i klartext i koden |

Inga credentials ligger i källkoden eller i git-historiken. Hemligheter hanteras via GitHub Secrets och refereras som miljövariabler i Container App.

## Kvarvarande risker

| Risk | Sannolikhet | Åtgärd |
|---|---|---|
| API saknar autentisering för slutanvändare | Hög | Implementera Entra ID eller API-nyckel per kund innan lansering |
| Ingen rate limiting på /verify | Medel | Lägg till throttling så endpointen inte kan missbrukas |
| Fast antal repliker skalar inte vid trafikspik | Medel | Aktivera autoskalning med maxtak |

## Kostnadskalkyl

| Resurs | SKU | Uppskattad kostnad/mån |
|---|---|---|
| Container Apps Environment | Consumption | ~0 kr i vila |
| Container App | Consumption, 2 repliker | ~50–100 kr |
| Azure Container Registry | Basic | ~50 kr |
| Azure Blob Storage | Standard LRS | <10 kr vid 8 000 certifikat/mån |
| **Totalt** | | **~150–200 kr/mån** |

Beräknat med 40 kunder och ca 8 000 certifikat per månad, enligt scenariots ekonomidata. Källa: Azure Pricing Calculator (uppskattning, ej exakt priskalkylator-export eftersom kontot saknade behörighet att deploya och mäta faktisk förbrukning).

### Skalningspunkt

Om certifikat-länkar delas i stor skala (t.ex. 10 000 anrop på en dag) är det inte kostnaden som är problemet — den stannar under en krona extra enligt kalkylatorn. Flaskhalsen blir istället att Container App bara har 2 fasta repliker och inte skalar upp automatiskt, vilket kan ge segare svarstider vid en verklig spik.


## Rekommendationer inför produktionssättning

1. Lös behörighetsfrågan så infrastrukturen faktiskt kan deployas till Azure
2. Implementera autentisering för slutanvändare innan publik lansering
3. Aktivera autoskalning istället för fast replikantal
4. Sätt kostnadslarm i Azure Cost Management

## Överlämning

| Leverabel | Plats |
|---|---|
| Källkod | https://github.com/SamuelThannan/labb3-certify |
| Bicep-mallar | /infra/ i repot |
| Pipeline-definition | .github/workflows/ci-cd.yml |
| API-dokumentation | http://localhost:5000/swagger (lokalt, ej skarp URL) |
| Denna rapport | RAPPORT.md i repots rot |
