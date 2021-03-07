# PaissaHouse
PaissaHouse is a simple plugin that runs in the background: when you view a housing ward from an aetheryte or ferry, it outputs any free houses it finds in that ward to the chat window.

## Usage
Simply install and it will happily run in the background.

Use `/psweep` to open configuration, and `/psweep reset` to reset the internal sweep cache if you want to sweep the same district multiple times in a row.

| Setting            | Description                                                                                          | Default |
|--------------------|------------------------------------------------------------------------------------------------------|---------|
| Enabled            | Whether or not the plugin is enabled. If disabled, it will not look for houses.                      | True    |
| Output Format      | The format results are displayed in.                                                                 | Simple  |

<figure>
  <img src="https://cdn.discordapp.com/attachments/263128686004404225/817965077982085120/unknown.png">
  <figcaption>The result of tabbing through all 24 wards in a few different districts.</figcaption>
</figure>


## FAQ

### Can this plugin automatically sweep for me?
**No.** It does not automate player actions in any way; you still have to teleport to each city, open the residential aethernet,
and click through each tab manually. This plugin simply outputs any unbought houses it finds in each ward to the chat log.

### Why is it named PaissaHouse?
Because it just sits there, silently, watching houses. Like a Paissa house.

![A Paissa house.](https://img2.finalfantasyxiv.com/accimg2/88/98/8898053ff4d9416da5a1a6a31d280ba42840161a.jpg)

## Acknowledgements

- [Dalamud](https://github.com/goatcorp/Dalamud) and [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)
- [Universalis](https://github.com/Universalis-FFXIV/Universalis) and Sonar for inspiration
