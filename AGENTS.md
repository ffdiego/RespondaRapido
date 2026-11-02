# Aplicação de perguntas e respostas

## Objetivo

Criar uma aplicação social de perguntas e respostas para uma tela grande. Os jogadores participam pelo celular; a tela grande apresenta a partida e seus resultados. A experiência deve seguir a identidade visual minimalista.

Este arquivo registra a intenção e as decisões já conhecidas. Não invente regras de jogo para preencher lacunas: converse com o usuário antes de definir mecânicas que alterem a experiência.

## Direção visual

- Fundo azul-marinho escuro: `#0e1922`.
- Painéis: `#182933`.
- Destaque menta: `#8ee5be`.
- Texto principal claro: `#eef4ed`.
- Texto secundário azul-acinzentado: `#96adb5`.
- Tipografia sem serifa, legível à distância, com títulos pesados e hierarquia clara.
- Use cartões arredondados, alto contraste, poucos elementos por tela e estados visuais evidentes.

## Arquitetura inicial sugerida

Discuta a arquitetura com o usuário antes de criar uma estrutura extensa. Como ponto de partida, prefira:

- uma aplicação anfitriã para a tela grande;
- uma interface web móvel servida localmente pelo anfitrião;
- comunicação em tempo real pela rede local;
- estado da partida e pontuação calculados e validados pelo servidor;
- configurações tipadas carregadas de `appsettings.json` e variáveis de ambiente, com uma seção e prefixo próprios do quiz.

Prefira por .NET 10, C#, ASP.NET Core/WebSockets. Reavalie se essa combinação serve ao quiz;

## Regras de integridade

- O servidor é a autoridade para jogadores conectados, fase da partida, respostas aceitas, pontuação e encerramento de cada pergunta.
- Não confie no cliente móvel para informar pontuação, validar prazo ou escolher qual resposta é correta.
- Evite enviar a resposta correta aos celulares antes de a pergunta ser encerrada, caso a mecânica aprovada tenha respostas secretas.
- Valide tamanho e formato de nomes, respostas e mensagens recebidas.
- Planeje reconexão somente depois de definir como identificar um jogador e se a sessão precisa sobreviver a recargas do navegador.

## Decisões que ainda precisam ser combinadas

Antes de fixar a mecânica, alinhe com o usuário:

- origem e formato do banco de perguntas;
- alternativas, respostas livres ou ambos;
- temporizador e comportamento para quem não responde;
- pontuação, desempates, equipes e quantidade de rodadas;
- quem escolhe/inicia perguntas e quais controles existem na tela grande;
- se jogadores podem entrar depois do início;
- se o app será apenas para rede local ou também para partidas pela internet;
- quais informações aparecem nos celulares antes, durante e depois de cada resposta.

Se alguma decisão não for necessária para uma etapa independente, avance com uma implementação pequena e reversível e deixe a hipótese documentada.

## Estilo de implementação

- Prefira componentes pequenos e nomes explícitos; mantenha a interface móvel simples e rápida.
- Preserve a compatibilidade com Windows e Linux quando a escolha técnica permitir.
- Mantenha o projeto construível sem a instalação do jogo original ou acesso ao Ghidra.
- Atualize o README desta pasta com comandos de execução e configuração assim que houver um projeto executável.
