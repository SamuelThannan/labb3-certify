# REFLEKTION_[dittnamn].md

### 1. Din roll i teamet

Jag jobbade ensam med scenario D, Certify, så jag ägde hela sprinten själv, från startkod till infrastruktur till dokumentation. Konkret: jag satte upp projektet i Visual Studio, fixade en trasig `.csproj`-fil som blev tom av misstag, och rensade upp en dubblett-mapp som orsakade `CS0579`-byggfel. Jag skrev hela `main.bicep` för ACR, Storage Account, Log Analytics, Container Apps Environment och Container App med Managed Identity och RBAC-roller. Jag skrev också GitHub Actions-pipelinen (`ci-cd.yml`) med tre jobb: bygg/test, push till ACR och deploy, samt ARCHITECTURE.md och den här rapporten.

### 2. Det svåraste momentet

Det svåraste var inte kod, det var att mitt Azure-konto saknade behörighet på resursgruppen. Jag fick `AuthorizationFailed` på allt från `az group show` till `az deployment group what-if` och `az ad sp create-for-rbac`. Jag testade flera saker för att vara säker: kollade om jag åtminstone kunde skapa resurser även om jag inte kunde läsa dem (`az deployment group create`), och om jag kunde skapa en Service Principal för pipelinen. Inget av det gick. Till slut accepterade jag att detta var en verklig begränsning utanför min kontroll, och fokuserade istället på att göra allt annat korrekt och dokumentera begränsningen tydligt istället för att låtsas att den inte fanns.

### 3. Vad förstår du nu som du inte förstod innan?

Jag förstår nu varför idempotens faktiskt spelar roll i praktiken, inte bara som ett ord från föreläsningen. Innan tänkte jag på Bicep som "ett sätt att skriva infrastruktur i kod istället för att klicka". Nu förstår jag att poängen är att Azure jämför det önskade tillståndet i mallen med det faktiska tillståndet i molnet varje gång man deployar, och bara ändrar det som skiljer sig. Det är därför man vågar köra samma deployment om och om igen utan att råka skapa dubbletter eller krascha något som redan funkar, systemet är designat för att vara säkert att upprepa, vilket är en helt annan sak än att bara "spara tid genom att slippa klicka".

### 4. Vad skulle du göra annorlunda?

Jag skulle ha verifierat min Azure-behörighet allra först, innan jag skrev en enda rad Bicep. Jag la flera timmar på att bygga en komplett infrastrukturmall innan jag ens testade `az deployment group what-if`, och fick då reda på att jag aldrig skulle kunna köra den skarpt. Hade jag kollat behörigheten direkt hade jag kunnat lägga om prioriteringen tidigare, fokusera mer på att göra den lokala demon (Docker, Swagger, pipeline-struktur) riktigt vass, istället för att hoppas att en live-deploy skulle gå att visa.

### 5. Arkitektur och ekonomi

Lösningen kör i Azure Container Apps med två fasta instanser, ett Container Registry för images och ett Storage Account för certifikatdata. Enligt Azures priskalkylator ligger den uppskattade kostnaden runt 150–200 kr i månaden vid 40 kunder och cirka 8 000 certifikat per månad, Container Apps och registryt är de största posterna, lagringen är i princip försumbar. Den mest sårbara punkten är inte kostnaden utan tillgängligheten: eftersom antalet repliker är fast och inte skalar automatiskt, riskerar en kraftig trafikspik, till exempel om en certifikatlänk delas viralt, att ge segare svarstider istället för en oväntat hög faktura. Det går att lösa genom att slå på autoskalning med ett maxtak, men det är inte implementerat i den här leveransen.
