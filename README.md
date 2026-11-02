# Responda Rápido

Jogo de perguntas para uma tela anfitriã e celulares conectados à mesma rede local. A tela anfitriã abre a sala, escolhe as matérias e inicia a partida; jogadores entram antes do início pelo link mostrado na tela.

## Requisitos

- .NET SDK 10
- Node.js 20 ou mais recente
- Celulares e computador anfitrião na mesma rede local

## Desenvolvimento

Abra dois terminais na pasta do projeto.

Terminal 1 — interface React/Vite (inicie primeiro):

```powershell
cd RespondaRapido/client
npm install
npm run dev
```

Terminal 2 — servidor e regras da partida:

```powershell
cd RespondaRapido
dotnet run
```
Ao iniciar o servidor .NET, o navegador abre `http://localhost:5173`, servido pelo Vite. Os celulares abrem `http://IP_DO_COMPUTADOR:5173/jogar`. O anfitrião e os jogadores devem estar na mesma rede; permita as portas 5173 e 5078 no firewall local quando necessário.

## Execução local empacotada

```powershell
cd RespondaRapido/client
npm install
npm run build
cd ..
dotnet run --no-launch-profile --urls http://0.0.0.0:5078
```

Abra `http://localhost:5078` na tela grande e use o endereço `http://IP_DO_COMPUTADOR:5078/jogar` nos celulares.

## Configuração

As opções ficam na seção `Quiz` de `RespondaRapido/appsettings.json` e podem ser definidas por variáveis de ambiente `Quiz__...`.

- `Quiz__AnswerSeconds`: duração da pergunta (padrão 20 segundos).
- `Quiz__QuestionsFile`: caminho do CSV (padrão `perguntas.csv`, relativo ao diretório do servidor).
- `Quiz__LobbyNameMinLength` e `Quiz__LobbyNameMaxLength`: limites de tamanho do nome.
- `Quiz__OpenBrowserOnStart`: abre o endereço configurado quando o servidor inicia.
- `Quiz__BrowserUrl`: endereço aberto (5173 em desenvolvimento, 5078 no perfil empacotado).

O CSV usa `;` e as colunas `NUMERO;PERGUNTA;RESP1;RESP2;RESP3;RESP4;CERTA;DIFI;MATERIA`. O carregador ignora linhas fora do formato. As matérias são C (Ciências), G (Geografia), H (História), I (Inglês), M (Matemática), P (Português) e V (Variedades). A escada de 16 perguntas usa níveis A nas cinco primeiras, B nas cinco seguintes, C nas cinco seguintes e D na pergunta de R$ 1 milhão.

## Organização

- `Domain/GamePolicies.cs`: escada de valores, pontuação, relógio de resposta e seleção de perguntas, todos substituíveis por interfaces.
- `Infrastructure/GameSession.cs`: sala em memória, jogadores, comandos e validações autoritativas do servidor.
- `Infrastructure/CsvQuestionCatalog.cs`: leitura e validação do banco.
- `client/src`: telas React do anfitrião e dos celulares.

A sala e os jogadores existem apenas enquanto o processo está em execução. Esta versão não retoma uma sessão depois de recarregar a página ou reiniciar o servidor. O anfitrião usa a tela principal como controle e não participa como jogador.