/* Page content remains visible without JavaScript. Each native preview is a local asset. */
const query = new URLSearchParams(location.search);
const exportMode = query.get('state') === 'print';
window.statusPage = function () {
  return {
    theme: query.get('state') === 'light' ? 'light' : 'dark',
    compact: query.get('state') === 'compact', copied: false, copyError: false,
    get previewImage() { return `images/skins/codex${this.theme === 'light' ? '-light' : ''}${this.compact ? '-compact' : ''}.png`; },
    async copyInstall() {
      const prompt = '请帮我安装这个 Codex 插件：https://github.com/MogooStudio/mogoo-codex-status';
      this.copyError = false;
      try {
        if (navigator.clipboard && window.isSecureContext) {
          await navigator.clipboard.writeText(prompt);
        } else {
          const field = document.createElement('textarea');
          field.value = prompt; field.style.position = 'fixed'; field.style.opacity = '0';
          document.body.appendChild(field); field.select();
          const succeeded = document.execCommand('copy'); field.remove();
          if (!succeeded) throw new Error('Copy unavailable');
        }
        this.copied = true;
        window.setTimeout(() => { this.copied = false; }, 4000);
      } catch { this.copyError = true; this.copied = false; }
    }
  };
};
window.addEventListener('DOMContentLoaded', () => {
  if (!window.gsap || !window.ScrollTrigger || exportMode) return;
  gsap.registerPlugin(ScrollTrigger);
  const media = gsap.matchMedia();
  media.add('(prefers-reduced-motion: no-preference)', () => {
    gsap.from('.entry-title', { y: 22, opacity: 0, duration: .65, ease: 'power3.out' });
    gsap.from('.entry-secondary', { y: 12, opacity: 0, stagger: .08, duration: .48, delay: .14, ease: 'power2.out' });
    gsap.from('.entry-product', { y: 28, opacity: 0, duration: .7, delay: .25, ease: 'power3.out' });
    document.querySelectorAll('.facts-reveal').forEach(section => {
      gsap.from(section.querySelectorAll('.fact, .reading-item'), {
        y: 18, stagger: .065, duration: .45, ease: 'power3.out',
        scrollTrigger: { trigger: section, start: 'top 92%', once: true }
      });
    });
    document.querySelectorAll('.appearance-reveal').forEach(section => {
      gsap.from(section, { scale: .96, duration: .6, ease: 'power3.out',
        scrollTrigger: { trigger: section, start: 'top 94%', once: true } });
    });
    document.querySelectorAll('.install-reveal').forEach(section => {
      gsap.from(section.querySelectorAll('.install-intro, .install-options'), {
        x: -14, stagger: .09, duration: .5, ease: 'power3.out',
        scrollTrigger: { trigger: section, start: 'top 94%', once: true }
      });
    });
    document.querySelectorAll('.story-stage').forEach(stage => {
      const story = gsap.timeline({ scrollTrigger: { trigger: stage, start: 'top 95%', end: 'bottom 25%', scrub: true } });
      story.fromTo(stage.querySelector('.story-full'), { x: 0, scale: 1 }, { x: 34, scale: .80, ease: 'none' }, 0);
      story.fromTo(stage.querySelector('.story-compact'), { x: 32, scale: .80 }, { x: 0, scale: 1, ease: 'none' }, 0);
    });
  });
});
