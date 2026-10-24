#!/usr/bin/env python3
"""
Generate a clean, professional, readable PDF guide for Practice 4 (Prediction, Reconciliation, Unity).
"""
from pathlib import Path
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, HRFlowable
)
from reportlab.pdfgen import canvas

# Fonts
FONT_REGULAR = '/System/Library/Fonts/Supplemental/Arial.ttf'
FONT_BOLD = '/System/Library/Fonts/Supplemental/Arial Bold.ttf'
FONT_ITALIC = '/System/Library/Fonts/Supplemental/Arial Italic.ttf'
FONT_BOLD_ITALIC = '/System/Library/Fonts/Supplemental/Arial Bold Italic.ttf'

pdfmetrics.registerFont(TTFont('Arial', FONT_REGULAR))
pdfmetrics.registerFont(TTFont('Arial-Bold', FONT_BOLD))
pdfmetrics.registerFont(TTFont('Arial-Italic', FONT_ITALIC))
pdfmetrics.registerFont(TTFont('Arial-BoldItalic', FONT_BOLD_ITALIC))
pdfmetrics.registerFontFamily('Arial', normal='Arial', bold='Arial-Bold', italic='Arial-Italic', boldItalic='Arial-BoldItalic')

# Colors
PRIMARY = colors.HexColor('#1E3A8A')     # Navy Blue
SECONDARY = colors.HexColor('#2563EB')   # Accent Blue
DARK_TEXT = colors.HexColor('#1F2937')   # Neutral Dark
LIGHT_BG = colors.HexColor('#F8FAFC')    # Soft Light
CARD_BG = colors.HexColor('#EFF6FF')     # Light Blue Card
BORDER_COL = colors.HexColor('#CBD5E1')  # Border
GREEN_ACCENT = colors.HexColor('#047857')# Dark Green Accent
ORANGE_ACCENT = colors.HexColor('#B45309')

styles = {
    'Title': ParagraphStyle('Title', fontName='Arial-Bold', fontSize=18, leading=22, textColor=PRIMARY, alignment=1, spaceAfter=4),
    'Subtitle': ParagraphStyle('Subtitle', fontName='Arial', fontSize=9.5, leading=13.5, textColor=colors.HexColor('#4B5563'), alignment=1, spaceAfter=8),
    
    'H1': ParagraphStyle('H1', fontName='Arial-Bold', fontSize=12, leading=15, textColor=PRIMARY, spaceBefore=8, spaceAfter=4, keepWithNext=True),
    'H2': ParagraphStyle('H2', fontName='Arial-Bold', fontSize=10, leading=13, textColor=SECONDARY, spaceBefore=6, spaceAfter=3, keepWithNext=True),
    
    'Body': ParagraphStyle('Body', fontName='Arial', fontSize=8.3, leading=11.2, textColor=DARK_TEXT, spaceAfter=3),
    'BodyBold': ParagraphStyle('BodyBold', fontName='Arial-Bold', fontSize=8.3, leading=11.2, textColor=DARK_TEXT, spaceAfter=3),
    'Bullet': ParagraphStyle('Bullet', fontName='Arial', fontSize=8.2, leading=11, textColor=DARK_TEXT, leftIndent=10, spaceAfter=2),
    
    'CmdBlock': ParagraphStyle('CmdBlock', fontName='Arial-Bold', fontSize=7.5, leading=9.5, textColor=colors.HexColor('#0F172A')),
    'TableHead': ParagraphStyle('TableHead', fontName='Arial-Bold', fontSize=7.8, leading=10, textColor=colors.white, alignment=1),
    'TableCell': ParagraphStyle('TableCell', fontName='Arial', fontSize=7.5, leading=9.5, textColor=DARK_TEXT),
    'TableCellBold': ParagraphStyle('TableCellBold', fontName='Arial-Bold', fontSize=7.5, leading=9.5, textColor=DARK_TEXT),
    'TableCellCenter': ParagraphStyle('TableCellCenter', fontName='Arial', fontSize=7.5, leading=9.5, textColor=DARK_TEXT, alignment=1),
    
    'QTitle': ParagraphStyle('QTitle', fontName='Arial-Bold', fontSize=8.8, leading=11.5, textColor=PRIMARY, keepWithNext=True),
    'QAnswer': ParagraphStyle('QAnswer', fontName='Arial', fontSize=8, leading=10.5, textColor=DARK_TEXT),
    'QShort': ParagraphStyle('QShort', fontName='Arial-Bold', fontSize=8, leading=10.5, textColor=GREEN_ACCENT),
}

class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_header_footer(num_pages)
            super().showPage()
        super().save()

    def draw_header_footer(self, page_count):
        self.saveState()
        self.setFont('Arial', 7.5)
        self.setFillColor(colors.HexColor('#64748B'))
        
        # Header (pages > 1)
        if self._pageNumber > 1:
            self.drawString(14*mm, 285*mm, "Методичка к сдаче ПР №4: Client-Side Prediction и Server Reconciliation")
            self.setStrokeColor(BORDER_COL)
            self.setLineWidth(0.5)
            self.line(14*mm, 283*mm, 196*mm, 283*mm)
            
        # Footer
        page_text = f"Стр. {self._pageNumber} из {page_count}"
        self.drawRightString(196*mm, 10*mm, page_text)
        self.drawString(14*mm, 10*mm, "Разработка многопользовательских приложений VR | Практическая работа №4")
        self.setStrokeColor(BORDER_COL)
        self.setLineWidth(0.5)
        self.line(14*mm, 12.5*mm, 196*mm, 12.5*mm)
        self.restoreState()

def build_pdf(target_path: Path):
    doc = SimpleDocTemplate(
        str(target_path),
        pagesize=A4,
        leftMargin=14*mm,
        rightMargin=14*mm,
        topMargin=15*mm,
        bottomMargin=15*mm
    )
    
    story = []
    
    # Title
    story.append(Paragraph("Методичка для подготовки к сдаче ПР №4", styles['Title']))
    story.append(Paragraph(
        "<b>Тема: Client-Side Prediction (CSP), Server Reconciliation и сетевая синхронизация</b><br/>"
        "Среда: Unity 6 (6000.3.8f1) + Netcode for GameObjects (NGO) 2.13.3 + macOS Standalone (.app)", 
        styles['Subtitle']
    ))
    story.append(HRFlowable(width="100%", thickness=1, color=PRIMARY, spaceAfter=6, spaceBefore=0))
    story.append(Paragraph(
        "Перед сдачей: соберите <code>PR4PredictionDemo.app</code>, запустите Host и Client, "
        "покажите T2, T3 и T5. HUD в standalone автоматически масштабируется от reference resolution 960×540, "
        "поэтому на Retina и большом окне остаётся читаемым.",
        styles['Body']
    ))
    
    # 1. ЧТО МЫ ДЕЛАЛИ
    story.append(Paragraph("1. Что мы сделали в этой практической работе", styles['H1']))
    story.append(Paragraph(
        "Мы создали сетевой интерактивный прототип на <b>Unity</b> для двух игроков, работающий по модели <b>«сервер — авторитет» (Server Authority)</b>. "
        "Главная цель работы — показать, как сетевая задержка (пинг) влияет на отзывчивость управления, и как методы <b>локального предсказания (Client-Side Prediction)</b> "
        "и <b>серверного исправления (Server Reconciliation)</b> устраняют задержку отклика, сохраняя полный контроль над игровым миром на сервере.",
        styles['Body']
    ))
    
    roles_data = [
        [Paragraph("Компонент", styles['TableHead']), Paragraph("Назначение в нашем коде", styles['TableHead'])],
        [Paragraph("<code>NetworkBootstrap</code>", styles['TableCellBold']), Paragraph("Точка входа, выбор роли (Host/Client), GUI для переключения профилей сети (0 мс, 100 мс, 175 мс) и флага CSP.", styles['TableCell'])],
        [Paragraph("<code>PredictedPlayer</code>", styles['TableCellBold']), Paragraph("Локальный сбор ввода, локальное применение (CSP), отправка RPC на сервер, получение серверного состояния, откат и повторное проигрывание (Reconciliation).", styles['TableCell'])],
        [Paragraph("<code>PredictionRules</code>", styles['TableCellBold']), Paragraph("Чистая физика и правила: нормализация вектора движения, проверка кулдауна рывка (1.25 с), расчёт смещения и коллизий.", styles['TableCell'])],
        [Paragraph("<code>NetworkLabSettings</code>", styles['TableCellBold']), Paragraph("Эмулятор сети: задаёт искусственную задержку (100–175 мс) и детерминированную потерю пакетов (5%).", styles['TableCell'])],
    ]
    t_roles = Table(roles_data, colWidths=[38*mm, 144*mm])
    t_roles.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), PRIMARY),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT_BG]),
        ('TOPPADDING', (0,0), (-1,-1), 2.5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 2.5),
        ('LEFTPADDING', (0,0), (-1,-1), 3),
        ('RIGHTPADDING', (0,0), (-1,-1), 3),
    ]))
    story.append(t_roles)
    
    # 2. КАК ЭТО РАБОТАЕТ
    story.append(Spacer(1, 4))
    story.append(Paragraph("2. Как это работает (Архитектура и алгоритмы)", styles['H1']))
    story.append(Paragraph(
        "<b>1) Важнейший принцип — Серверный авторитет (Server Authority):</b><br/>"
        "Клиент <u>никогда</u> не передаёт серверу свои координаты <code>(x, y, z)</code>. Клиент отправляет через RPC только <b>своё намерение (Input Intent)</b>: "
        "порядковый номер <code>sequence</code>, вектор движения <code>move</code> и флаг <code>dashRequested</code>. "
        "Сервер сам рассчитывает новую позицию, проверяет кулдаун рывка и коллизии с препятствиями. Игрок не может телепортироваться или взломать позицию.",
        styles['Body']
    ))
    story.append(Paragraph(
        "<b>2) Клиентское предсказание (Client-Side Prediction):</b><br/>"
        "Обычно при задержке 100 мс игрок нажимает клавишу и ждёт 200 мс (RTT туда и обратно), пока сервер ответит. Это делает управление «ватным». "
        "С включённым CSP клиент <b>мгновенно применяет движение локально</b> (отклик <b>0.0 мс</b>) и сохраняет этот ввод в список <code>_pendingInputs</code>. "
        "Для игрока всё работает без малейшей задержки.",
        styles['Body']
    ))
    story.append(Paragraph(
        "<b>3) Серверная сверка (Server Reconciliation):</b><br/>"
        "Сервер рассчитывает физику для каждого шага и присылает клиенту авторитетный ответ (ACK с подтверждённым номером <code>sequence</code> и эталонной позицией).<br/>"
        "Клиент при получении ответа:<br/>"
        "• Удаляет из списка <code>_pending</code> все ходы, которые сервер уже подтвердил.<br/>"
        "• Сверяет позицию: если предсказанная клиентом позиция разошлась с сервером (например, врезался в стену или нарушил кулдаун):<br/>"
        "&nbsp;&nbsp;1. Клиент <b>сбрасывает позицию</b> персонажа на авторитетную позицию сервера.<br/>"
        "&nbsp;&nbsp;2. <b>Повторно накатывает (re-play)</b> все оставшиеся неподтверждённые ходы из <code>_pending</code>.<br/>"
        "В результате игрок остаётся точно там, где разрешил сервер, без рассинхронизации и рывков.",
        styles['Body']
    ))
    
    # 3. КАК ЗАПУСКАТЬ
    story.append(Spacer(1, 4))
    story.append(Paragraph("3. Как запускать проект на Mac (Шпаргалка команд)", styles['H1']))
    story.append(Paragraph(
        "Бинарный файл собран в нативный macOS бандл. Чтобы открыть **два независимых окна** на Mac, запускайте бинарник напрямую из двух вкладок Терминала:",
        styles['Body']
    ))
    
    run_data = [
        [Paragraph("Роль", styles['TableHead']), Paragraph("Команда в Терминале Mac", styles['TableHead']), Paragraph("Что происходит", styles['TableHead'])],
        [
            Paragraph("<b>Окно 1: Сервер (Host)</b>", styles['TableCellBold']),
            Paragraph("<code>./unity/PredictionDemo/Builds/PR4PredictionDemo.app/Contents/MacOS/PredictionDemo</code><br/><i>(в окне нажмите кнопку <b>«Start Host»</b>)</i>", styles['CmdBlock']),
            Paragraph("Запускается окно хоста: сервер слушает порт и управляет первым персонажем.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Окно 2: Клиент (Client)</b>", styles['TableCellBold']),
            Paragraph("<code>./unity/PredictionDemo/Builds/PR4PredictionDemo.app/Contents/MacOS/PredictionDemo</code><br/><i>(в окне нажмите кнопку <b>«Start Client»</b>)</i>", styles['CmdBlock']),
            Paragraph("Открывается второе окно клиента и подключается к первому игроку.", styles['TableCell'])
        ]
    ]
    t_run = Table(run_data, colWidths=[35*mm, 105*mm, 42*mm])
    t_run.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), PRIMARY),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT_BG]),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 3),
        ('RIGHTPADDING', (0,0), (-1,-1), 3),
    ]))
    story.append(t_run)
    story.append(Spacer(1, 3))
    story.append(Paragraph(
        "<b>Проверка интерфейса.</b> В standalone откройте окно размером 1920×1080 или включите полный экран. "
        "Панель остаётся в левом верхнем углу; должны быть читаемы заголовок, Status/Role, Start Host/Start Client, "
        "профили 0 ms / 0%, 100 ms / 0%, 175 ms / 5%, Prediction и telemetry. Масштабирование влияет только на отрисовку HUD и не меняет сетевую логику.",
        styles['Body']
    ))
    
    # 4. ЧТО ПОКАЗАТЬ ПРЕПОДАВАТЕЛЮ
    story.append(Spacer(1, 4))
    story.append(Paragraph("4. Сценарий демонстрации: что конкретно показывать преподавателю", styles['H1']))
    story.append(Paragraph("Демонстрация занимает 3 минуты и показывает 4 ключевых этапа задания:", styles['BodyBold']))
    
    demo_steps = [
        ("Шаг 1. Базовое перемещение двух игроков (Тест T1)", 
         "Подвигайте персонажей на WASD в обоих окнах. Покажите, что оба игрока видят перемещения друг друга на экране."),
        ("Шаг 2. Задержка без предсказания (Тест T2, Prediction: OFF)", 
         "В окне Клиента нажмите кнопку профиля сети <b>«100 ms / 0%»</b> и нажмите кнопку <b>«Prediction: OFF»</b>. "
         "Нажмите клавишу <code>Space</code> (рывок). <b>Обратите внимание преподавателя на HUD:</b> в строке <i>«Last dash reaction»</i> появится задержка <b>~210 мс</b>. "
         "Персонаж делает рывок с заметным запозданием, так как ждёт ответа сервера."),
        ("Шаг 3. Мгновенный отклик с предсказанием (Тест T3, Prediction: ON)", 
         "Нажмите <b>«Prediction: ON»</b> (сеть остаётся 100 мс). Нажмите <code>Space</code>. "
         "<b>Покажите HUD:</b> строка <i>«Last dash reaction»</i> покажет <b>0.0 мс</b>! Персонаж сорвался с места мгновенно, хотя пинг остался 200 мс RTT. Сервер подтвердил этот рывок через ACK."),
        ("Шаг 4. Нагрузка сети (Тест T4)",
         "Выберите профиль <b>«175 ms / 5%»</b>, оставьте <b>Prediction: ON</b> и выполните обычное движение и два dash с паузой между ними. "
         "Покажите, что управление остаётся отзывчивым, а сервер подтверждает допустимые действия даже при детерминированных logical drops."),
        ("Шаг 5. Серверная коррекция расхождения / читерства (Тест T5, Reconciliation)", 
         "Нажмите клавишу <b><code>B</code></b> (это специальный тестовый ввод — попытка сделать нелегальный двойной рывок в обход правил). "
         "Клиент локально предскажет второй рывок, но сервер отклонит запрещённое действие по cooldown (<code>REJECT: cooldown</code>). "
         "<b>Покажите HUD:</b> строка <i>«Last reconciliation error»</i> зафиксирует ошибку <b>~0.535 м</b>. Персонажа мягко скорректирует на место, разрешённое сервером!"),
    ]
    
    for title, desc in demo_steps:
        box = [
            Paragraph(f"<b>{title}</b>", styles['BodyBold']),
            Paragraph(desc, styles['Body'])
        ]
        t_box = Table([[box]], colWidths=[182*mm])
        t_box.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), CARD_BG),
            ('BOX', (0,0), (-1,-1), 0.5, SECONDARY),
            ('TOPPADDING', (0,0), (-1,-1), 3),
            ('BOTTOMPADDING', (0,0), (-1,-1), 3),
            ('LEFTPADDING', (0,0), (-1,-1), 5),
            ('RIGHTPADDING', (0,0), (-1,-1), 5),
        ]))
        story.append(t_box)
        story.append(Spacer(1, 2))

    # 5. Финальная проверка
    story.append(Spacer(1, 4))
    story.append(Paragraph("5. Финальная проверка перед сдачей", styles['H1']))
    final_data = [
        [Paragraph("Что проверить", styles['TableHead']), Paragraph("Готовый результат", styles['TableHead'])],
        [Paragraph("Unity EditMode", styles['TableCellBold']), Paragraph("6/6 passed, failed=0.", styles['TableCell'])],
        [Paragraph("Регрессия ПР №1–№3", styles['TableCellBold']), Paragraph("78/78 passed, failed=0.", styles['TableCell'])],
        [Paragraph("macOS standalone", styles['TableCellBold']), Paragraph("PR4PredictionDemo.app собран и smoke-launch выполнен.", styles['TableCell'])],
        [Paragraph("HUD", styles['TableCellBold']), Paragraph("Проверен в Editor Game View, 960×540, 1920×1080 и большом полноэкранном окне; UI читаем.", styles['TableCell'])],
        [Paragraph("Артефакты", styles['TableCellBold']), Paragraph("README.md, Prediction_Implementation.md, Prediction_Test_Matrix.md и три скриншота в docs/media/.", styles['TableCell'])],
    ]
    t_final = Table(final_data, colWidths=[52*mm, 130*mm])
    t_final.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), PRIMARY),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT_BG]),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_final)
        
    # 6. ВОПРОСЫ ДЛЯ ЗАЩИТЫ
    story.append(Spacer(1, 4))
    story.append(Paragraph("6. Ответы на контрольные вопросы преподавателя", styles['H1']))
    
    qa = [
        (
            "1. Почему сервер не должен безусловно доверять клиентской позиции (ActorLocation / Transform)?",
            "Если сервер принимает координаты напрямую от клиента, любой клиент может модифицировать память и телепортироваться сквозь стены (speedhack, teleportation). Сервер должен получать только физический ввод (намерение) и сам рассчитывать перемещение.",
            "Иначе игра уязвима к читам на телепортацию и перемещение сквозь коллизии."
        ),
        (
            "2. В чём разница между Client-Side Prediction и Server Reconciliation?",
            "Prediction — это шаг вперёд: клиент сразу применяет свой ввод локально, не дожидаясь ответа сервера, чтобы игрок не чувствовал задержки. Reconciliation — это шаг сверки: клиент получает эталонную позицию сервера, и если локальный прогноз ошибся, сбрасывает координаты к серверным и заново накатывает накопленные ходы.",
            "Prediction устраняет задержку отклика, а Reconciliation исправляет ошибки предсказания по эталону сервера."
        ),
        (
            "3. Что такое Saved Move и зачем они хранятся в буфере pending?",
            "Saved Move — это структура с локальным вводом (sequence, dt, move, dash), отправленная серверу, но ещё не подтверждённая. Они нужны, чтобы в случае серверной коррекции не сбросить игрока в прошлое, а мгновенно пересчитать его актуальную позицию поверх авторитетного состояния.",
            "Чтобы после серверного отката быстро проиграть ещё не подтверждённые ходы игрока."
        ),
    ]
    
    for q, a_full, a_short in qa:
        q_box = [
            Paragraph(f"<b>{q}</b>", styles['QTitle']),
            Paragraph(a_full, styles['QAnswer']),
            Paragraph(f"<b>Коротко у доски:</b> {a_short}", styles['QShort'])
        ]
        t_q = Table([[q_box]], colWidths=[182*mm])
        t_q.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), LIGHT_BG),
            ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
            ('TOPPADDING', (0,0), (-1,-1), 3),
            ('BOTTOMPADDING', (0,0), (-1,-1), 3),
            ('LEFTPADDING', (0,0), (-1,-1), 5),
            ('RIGHTPADDING', (0,0), (-1,-1), 5),
        ]))
        story.append(t_q)
        story.append(Spacer(1, 2))
        
    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"Guide PDF generated at: {target_path}")

if __name__ == '__main__':
    root = Path(__file__).resolve().parent.parent
    target = root / "МЕТОДИЧКА_ПР4_СДАЧА.pdf"
    build_pdf(target)
