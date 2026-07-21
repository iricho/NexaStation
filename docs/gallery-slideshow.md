# Gallery slideshow

Slideshows operate over an explicit ordered set of available images. Interval, loop, shuffle, transition preference, and optional video inclusion are typed settings. The service supports pause/resume and observes application foreground state so it does not continue advancing invisibly.

Navigation wraps only when loop is enabled. Empty or exhausted sequences stop cleanly, and cancellation releases the running task. Phase 5 does not play video inline; allowing videos delegates them to the media-launch path.

Transition choice is represented in state, but the initial WinUI surface uses restrained transitions. Reduced-motion system integration and automatic coordination with every non-Gallery launch remain future hardening work.
