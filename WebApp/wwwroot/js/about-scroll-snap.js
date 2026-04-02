const CLASS_NAME = "about-scroll-snap";

function toggle(enabled) {
  document.documentElement.classList.toggle(CLASS_NAME, enabled);
  document.body.classList.toggle(CLASS_NAME, enabled);
}

export function enable() {
  toggle(true);
}

export function disable() {
  toggle(false);
}
