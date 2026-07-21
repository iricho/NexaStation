# Owner PIN

PINs contain four to eight digits and are stored as a random-salt PBKDF2-SHA256 verifier (210,000 iterations by default). Verification uses fixed-time comparison and escalating lockouts after repeated failures.
