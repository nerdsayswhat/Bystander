# Bystander

A hands-only VR narrative piece for the Meta Quest 3, by Ashwin Fulfagar.

Bystander is an interactive VR film made as a final major project for an MA in
Virtual Reality. The viewer stands in one place, in darkness and in chains,
using only their hands. While a loud spectacle holds everyone's attention,
small problems happen close by: an old man's oranges spill, a can rolls in
from behind, a woman is frightened by a barking dog. The viewer can reach for
them or leave them alone. The piece asks whether you will act, or remain a
bystander.

## Copyright

Copyright (c) 2026 Ashwin Fulfagar. All rights reserved.

This repository is public so that the work can be viewed. It is **not open
source**. You may not copy, modify, distribute or reuse any part of it, in any
project, without written permission. See [LICENSE](LICENSE).

## About the project

- **Platform:** Meta Quest 3, hand tracking only (no controllers)
- **Engine:** Unity 6000.3.8f1 (URP), OpenXR, Meta XR SDK 207
- **Length:** about eight and a half minutes with no action
- **Look:** people and the dog are faceless clouds of particles; the street is
  drawn in white lines and seen from above
- **Interaction:** reach, bend and hold to break the chains, then pick up,
  give and drop in; head direction lights what the viewer looks at
- **Accessibility:** a seated mode, subtitles for spoken lines, and a
  photosensitivity warning before the opening flashes

## Opening the project

1. Install Unity 6000.3.8f1 with Android Build Support.
2. Install [Git LFS](https://git-lfs.com/) before cloning, because models,
   textures and audio are stored with it.
3. Open the project folder in Unity. Packages (including Meta XR SDK 207)
   restore from `Packages/manifest.json`.
4. The film runs from one scene, `Scene 2`, through a scene sequencer.
5. Build for Android and run on a Quest 3 with hand tracking enabled.

## Credits

- Concept, design, script, build and performance: Ashwin Fulfagar
- Motion capture technician: Shrey Bansal
- Special thanks: Dr. Ana-Desipina Tudor, Eric Fanghanel, Klaus Fried

## Third-party material

Some files in this repository belong to other people and are used under their
own licences or terms. They are not covered by this project's copyright
notice, are not licensed for reuse from here, and remain the property of their
owners.

- **Mixamo animations** (Adobe), used for the crowd and the street
- **Sound effects** from Pixabay, used under the Pixabay Content Licence
- **Spoken dialogue lines**, generated with ElevenLabs
- **Meta XR SDK** and Meta Interaction SDK, used under Meta's terms
- **Jost typeface**, used under the SIL Open Font Licence
- **Unity** packages, used under Unity's terms

If you own any material here and want it credited differently or removed,
please contact the repository owner.
