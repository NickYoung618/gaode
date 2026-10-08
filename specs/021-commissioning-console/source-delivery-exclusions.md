# Source delivery without prototype sample images

User confirmed on 2026-10-08 that prototype sample images must not be pushed. This delivery contains the current source, configuration definitions, contracts, tasks, maintenance scripts and non-secret verification evidence; 26 JPEG/PNG resources in frontend/src/assets are omitted. The original local commits and image files remain intact, with a rollback tag. The source delivery is based on local commit 333baf4ef7e9d3f3a42ae845c4a2bd9e58aec19f.

Images remain in the verified final-4 deployment ZIP (SHA256 BD057E5820D0884653CF291E429599B0FB5FCFCC74A2074486138C55661855B8) and installed final-3 directory. No binary patch or deployment change is needed. To reproduce the full approved frontend offline, restore the image files from app/frontend/assets in that same package into frontend/src/assets, preserving their names and bytes. Missing images must not be replaced with fabricated validation results; prototype validation requires the restored resources.

The source delivery commit uses the existing remote 019 source as its parent so omitted images are not pulled into Git through unpublished local history. It does not rewrite local source history, alter the customer prototype or remove deployed resources.

40 backend checks, 3 frontend component checks and prototype verification passed before resource omission; this is publication filtering only. Real-device complete flow and desktop recovery acceptance remain unverified. The historical report that Git push completed must be read together with the actual remote verification record: the earlier complete upload failed with HTTP 408.
