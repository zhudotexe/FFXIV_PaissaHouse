# PaissaHouse

Looking for that perfect house? PaissaHouse lets you receive notifications when a new plot of land is up for sale on
your home world, and contribute to PaissaDB when viewing housing wards at a city aetheryte/ferry.

## Installation

PaissaHouse is a Dalamud plugin, and should be installed
using [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher).
*Installing the plugin using the GitHub Releases tab or by downloading the code is not recommended or supported.*

If PaissaHouse does not appear under the list of available plugins, ensure that plugin testing is enabled in your
plugins settings menu.

## Usage

Simply install and it will happily run in the background, notifying you when a new plot of land is up for sale on your
home world.

Use `/psweep` to open configuration, if you'd like to receive notifications for other worlds or only for certain
districts.

## Contributing

PaissaHouse relies on data contributions from players like you to broadcast the latest housing data.

### Ward Sweeps

To contribute data to PaissaDB, the API powering PaissaHouse, simply teleport to a major city aetheryte, select
"Residential District Aethernet" -> "Go to Specified Ward (Review Tabs)" and click through the 24 wards. This is
called "sweeping" a district.

If all goes well, you'll see any new plots you found pop up in real time, and a summary of the district once you've
clicked through the wards!

<figure>
  <img src="https://cdn.discordapp.com/attachments/263128686004404225/842268996886724648/unknown.png">
  <figcaption>The result of tabbing through all 24 wards.</figcaption>
</figure>

If you plan on sweeping the same district multiple times in a row, run `/psweep reset` between each sweep to reset the
internal sweep cache.

### Lottery Sweeps

To contribute information about a plot on sale via lottery, simply view the plot's placard by teleporting to the ward,
walking up to the plot, and right-clicking on the placard. This will help ensure that we have up-to-date lottery counts
for each plot!

## Custom Output Format

If using `Custom` output format, use these variables surrounded with curly braces to customize your output (
e.g. `{worldName}`):

| Variable               | Description                                                       | Example           |
|------------------------|-------------------------------------------------------------------|-------------------|
| `districtName`         | The name of the housing district.                                 | Lavender Beds     |
| `districtNameNoSpaces` | The name of the housing district, with no spaces.                 | LavenderBeds      |
| `worldName`            | The name of the world.                                            | Diabolos          |
| `wardNum`              | The ward number.                                                  | 24                |
| `plotNum`              | The plot number.                                                  | 60                |
| `housePrice`           | The price of the house as a raw number.                           | 3750000           |
| `housePriceMillions`   | The price of the house divided by 1 million, to 3 decimal places. | 3.750             |
| `houseSizeName`        | The name of the size of the house.                                | Medium            |

Example (Pings output format):

```
@{houseSizeName}{districtNameNoSpaces} {wardNum}-{plotNum} ({housePriceMillions}m)
```

## FAQ

### Does this plugin automatically search for houses for me?

**No.** It does not automate player actions in any way; it depends on other players' contributions to housing data to
send updates on new plots.

If you're contributing, you still have to teleport to each city, open the residential aethernet, and click through each
tab manually. Want to contribute? Check out *Contributing*, above.

### Why is it named PaissaHouse?

Because it just sits there, silently, watching houses. Like a Paissa house.

![A Paissa house.](https://img2.finalfantasyxiv.com/accimg2/88/98/8898053ff4d9416da5a1a6a31d280ba42840161a.jpg)

## Acknowledgements

- [Dalamud](https://github.com/goatcorp/Dalamud) and [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)
- [Universalis](https://github.com/Universalis-FFXIV/Universalis) and Sonar for inspiration
