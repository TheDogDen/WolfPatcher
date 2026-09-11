# Nexus Mods publication — HOTF PT-BR 1.0.0

This document contains the canonical end-user text for the existing Nexus Mods page:

https://www.nexusmods.com/werewolftheapocalypseheartoftheforest/mods/2

Do not create a duplicate mod page. The existing HOTF PT-BR page should remain the public home of the localization.

## Recommended title

**Werewolf - The Apocalypse Heart of the Forest PT-BR Completo v1.0 — WolfPatcher**

## Short description

Localização completa de Werewolf: The Apocalypse — Heart of the Forest para português brasileiro. Instalação e restauração seguras pelo WolfPatcher, com validação por SHA-256, backup automático e operação totalmente offline.

## Main description

### Localização PT-BR completa

Localização completa de **Werewolf: The Apocalypse — Heart of the Forest** para português brasileiro (PT-BR).

A tradução cobre narrativa, diálogos, escolhas, objetivos, notificações, menus, configurações, acessibilidade, pausa, Ficha de Personagem, HUD, atributos, mapa, nomes de locais, saves, tooltips, mensagens de sistema, títulos e demais textos exibidos ao jogador.

A narrativa foi revisada contra os arquivos originais em inglês. A terminologia relacionada ao cenário de **Lobisomem: O Apocalipse** segue como referência prioritária a edição brasileira oficial de **Lobisomem: O Apocalipse W5 — Livro Básico**.

A versão 1.0.0 passou por auditoria dos textos e arquivos modificados, validação estrutural e QA diretamente no jogo.

### Instalação pelo WolfPatcher

A distribuição atual usa o **WolfPatcher**, em vez da substituição manual de arquivos completos do jogo.

1. Baixe `WolfPatcher_HOTF_PTBR_1.0.0.zip`.
2. Extraia **todo o conteúdo do ZIP** para uma pasta comum do Windows.
3. Execute `WolfPatcher.Gui.exe`.
4. Se mais de uma instalação for encontrada, selecione a instalação desejada. Também é possível selecionar manualmente a pasta do jogo.
5. Clique em **Instalar localização PT-BR**.
6. O WolfPatcher verifica os hashes dos arquivos-base antes de qualquer alteração, cria backup local, aplica os deltas e valida os hashes finais.

Não é necessário instalar o .NET separadamente.

### Restaurar / desinstalar

Execute novamente `WolfPatcher.Gui.exe` e use **Restaurar arquivos originais**.

A restauração usa o backup local criado antes da instalação.

### Compatibilidade

A loja é usada apenas para ajudar a localizar a instalação. A compatibilidade é determinada pelos **hashes reais dos arquivos do jogo**, não pelo nome da loja ou da pasta.

Steam, GOG, Epic Games Store ou uma instalação selecionada manualmente só são aceitas quando os arquivos correspondem a uma baseline suportada.

A baseline atualmente validada tem proveniência Steam:

- Steam AppID: `1342620`
- BuildID: `5703970`
- DepotID: `1342621`
- ManifestID: `7391116612331907759`

Uma instalação de outra loja com exatamente os mesmos arquivos-base poderá ser aceita pelos hashes. Isso não deve ser interpretado como garantia de compatibilidade com toda e qualquer build GOG ou Epic.

Builds desconhecidas ou arquivos-base modificados são recusados **antes de qualquer alteração**.

### Segurança e privacidade

WolfPatcher funciona completamente offline durante a instalação.

O programa:

- não possui telemetria;
- não baixa arquivos;
- não envia arquivos ou informações do usuário;
- não instala serviços;
- não cria persistência em segundo plano;
- não modifica Microsoft Defender, Firewall ou SmartScreen;
- solicita UAC apenas quando a pasta de destino exige privilégios administrativos.

O código-fonte first-party do WolfPatcher está disponível publicamente para inspeção e verificação de build:

https://github.com/TheDogDen/WolfPatcher

Os cinco principais artefatos first-party do pacote público foram reproduzidos byte a byte a partir do source arquivado, com o procedimento documentado no repositório.

### Microsoft SmartScreen e antivírus

O executável não tenta contornar ou desativar o Microsoft SmartScreen.

Executáveis novos ou não assinados podem receber aviso de reputação do SmartScreen mesmo quando não existe detecção de malware. A decisão de executar permanece sempre sob controle do usuário.

Se um antivírus sinalizar o pacote, **não desative sua proteção para forçar a execução**. Verifique o SHA-256 do arquivo baixado, faça nova análise com sua ferramenta de segurança e consulte o código-fonte público antes de decidir prosseguir.

### SHA-256 do pacote oficial

Arquivo:

`WolfPatcher_HOTF_PTBR_1.0.0.zip`

Tamanho:

`104,198,215` bytes

SHA-256:

`619b2d0b6ebe817611736d2e948035e857ed13f4caf13773b1f296f49f5b0ab9`

Hash do executável principal `WolfPatcher.Gui.exe`:

`1c54b9b519d42b6b5d8cd3e60ffd9adf5a5a7cff8ebe3e0e8929d6480507aac0`

Mais hashes verificados:

https://github.com/TheDogDen/WolfPatcher/blob/main/RELEASE_HASHES.md

### Conteúdo do pacote

O pacote público contém o WolfPatcher, os deltas necessários para a localização, metadados de compatibilidade e as ferramentas de terceiros necessárias em suas respectivas licenças.

Ele **não contém arquivos completos do jogo**.

### Conflitos com outros mods

Mods que alterem os mesmos arquivos do jogo podem tornar a baseline incompatível ou entrar em conflito com a localização. Nesse caso o WolfPatcher poderá recusar a instalação para evitar modificar uma build que não reconhece.

### Créditos e direitos

Localização PT-BR e WolfPatcher: **TheDogDen**.

Esta é uma localização não oficial feita por fã e requer uma cópia legítima de **Werewolf: The Apocalypse — Heart of the Forest**.

Werewolf: The Apocalypse, Heart of the Forest, World of Darkness e todo o conteúdo original do jogo, marcas, personagens, cenários e assets pertencem aos respectivos titulares. Este projeto não é afiliado nem endossado pelos desenvolvedores, publishers, Paradox Interactive ou World of Darkness.

O código first-party do WolfPatcher é disponibilizado publicamente para transparência, inspeção de segurança e verificação de build; consulte `LICENSE.md` no repositório para as condições de uso.

## Nexus file management

Recommended primary file:

- `WolfPatcher_HOTF_PTBR_1.0.0.zip`
- version: `1.0.0`
- category/status: Main File / recommended
- SHA-256: `619b2d0b6ebe817611736d2e948035e857ed13f4caf13773b1f296f49f5b0ab9`

The previous full-file/manual-install packages should be moved to **Old Files / Archived** or otherwise removed from normal recommended distribution. They should not remain the primary installation method because the current release is designed to distribute deltas and operate with backup, compatibility validation and restoration.

Do not claim blanket GOG/Epic compatibility unless additional baselines are independently validated. Store detection and build compatibility are separate concepts.
