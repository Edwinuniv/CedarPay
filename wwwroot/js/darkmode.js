function toggleDarkMode() {
    var html = document.documentElement;
    var isDark = html.classList.toggle('dark-mode');
    document.body.classList.toggle('dark-mode', isDark);
    localStorage.setItem('cedarpay-dark', isDark ? '1' : '0');
    var btn = document.getElementById('darkModeBtn');
    if (btn) {
        btn.innerHTML = isDark
            ? '<i class="bi bi-sun"></i>'
            : '<i class="bi bi-moon"></i>';
    }
}

document.addEventListener('DOMContentLoaded', function () {
    var isDark = localStorage.getItem('cedarpay-dark') === '1';
    document.documentElement.classList.toggle('dark-mode', isDark);
    document.body.classList.toggle('dark-mode', isDark);
    var btn = document.getElementById('darkModeBtn');
    if (btn) {
        btn.innerHTML = isDark
            ? '<i class="bi bi-sun"></i>'
            : '<i class="bi bi-moon"></i>';
    }
});