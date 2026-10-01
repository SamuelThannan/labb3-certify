1. Container Apps — Varför Container Apps och inte AKS?

Vi körde Container Apps eftersom det här är ett litet projekt, ett API med några få endpoints, och det kändes överdrivet att sätta upp ett helt Kubernetes-kluster för det. Azure sköter skalning och drift åt en i Container Apps, så man slipper konfigurera noder och nätverk själv. Nackdelen är att man tappar kontroll, man kommer inte åt klustret direkt och kan inte köra kubectl eller sätta upp avancerade nätverksregler. Om projektet hade varit större, typ flera tjänster som pratar med varandra och behövde mer skräddarsydd skalning, hade AKS varit bättre.

2. CI/CD — Pipeline-flödet steg för steg

Pipelinen körs i GitHub Actions och triggas vid push till main. Först bygger och testar den koden (build-and-test). Går det bra fortsätter den till push-to-acr, som bygger en Docker-image och pushar den till Container Registry. Sista steget, deploy, uppdaterar Container App med den nya imagen. Misslyckas testerna stannar allt direkt tack vare needs, så trasig kod aldrig når längre. I vårt fall blev bara build-and-test grönt, resten av stegen kräver Azure-inloggning som vårt konto inte hade behörighet till.

3. IaC — Varför Bicep istället för portalen? Vad är idempotens?

Bicep gör att infrastrukturen finns som kod istället för att bara finnas i ens minne eller som klick i portalen. Man kan se exakt vad som skapats, versionshantera det i git, och återanvända det. Idempotens betyder att man kan köra samma mall flera gånger utan att det blir fel eller dubbletter — Azure jämför bara med vad som redan finns och ändrar det som skiljer sig. Det gör att man vågar deploya om utan att oroa sig för att krascha något som redan funkar. Vi kunde dock aldrig testa detta skarpt eftersom kontot saknade behörighet att deploya, men mallen är i alla fall syntaktiskt korrekt (verifierat med az bicep build).

4. Säkerhet — Hur hanterar ni hemligheter och credentials?

Inga lösenord eller nycklar ligger hårdkodade i koden. Container App har en Managed Identity som fått rättigheterna AcrPull mot registryt och Storage Blob Data Contributor mot storage-kontot, så den autentiserar sig automatiskt mot Azure. I pipelinen ligger Azure-inloggningen som en GitHub Secret istället för i klartext i YAML-filen. Hade vi hårdkodat en nyckel direkt i koden hade vem som helst med tillgång till repot kunnat använda den för att komma åt resurserna eller orsaka kostnader, och råkar den väl hamna i git-historiken ligger den kvar där även om man tar bort raden senare.

5. Ekonomi — Vad händer om certifikat-länken delas 10 000 gånger på en dag?

Enligt Azures priskalkylator skulle 10 000 anrop kosta under en krona extra, eftersom både Container Apps och Blob Storage debiteras per faktisk förbrukning och varje anrop är litet (läser en blob, returnerar lite JSON). Det stora problemet blir inte kostnaden utan att appen bara har 2 fasta repliker och inte skalar automatiskt, så en kraftig spik riskerar att ge segare svar eller fel istället för en chockräkning. En lösning hade varit att slå på autoskalning med ett maxtak, cacha svaren för populära certifikat, och sätta ett kostnadslarm så man märker om något sticker iväg.