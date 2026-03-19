-- ============================================
-- SEED: Datos de prueba completos para inventario
-- Ejecutar DESPUES de bloque6_cleanup.sql
-- ============================================

-- ============================================
-- 1. CATEGORIAS (8)
-- ============================================
INSERT INTO inventory_categories (name, description, color, display_order, created_by) VALUES
    ('TORNILLERIA',    'Tornillos, tuercas, arandelas y pernos',           '#3B82F6', 1, 1),
    ('CABLEADO',       'Cables electricos, de datos y de control',         '#10B981', 2, 1),
    ('CONECTORES',     'Conectores industriales, terminales y bornes',     '#8B5CF6', 3, 1),
    ('HERRAMIENTAS',   'Herramientas manuales y electricas',               '#F59E0B', 4, 1),
    ('SENSORES',       'Sensores de proximidad, temperatura y presion',    '#EC4899', 5, 1),
    ('MOTORES',        'Motores AC, DC, paso a paso y servomotores',       '#EF4444', 6, 1),
    ('NEUMATICA',      'Cilindros, valvulas y conexiones neumaticas',      '#06B6D4', 7, 1),
    ('ELECTRONICA',    'PLCs, fuentes, relevadores y componentes',         '#84CC16', 8, 1);

-- ============================================
-- 2. PRODUCTOS
-- ============================================

-- TORNILLERIA (cat 1) - 10 productos
INSERT INTO inventory_products (category_id, code, name, description, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (1, 'TOR-001', 'Tornillo M3x10',      'Cabeza Phillips, acero inoxidable',    150,  50, 'pza', 0.50, 'A-1', 1),
    (1, 'TOR-002', 'Tornillo M4x15',      'Cabeza Phillips, acero galvanizado',    20,  30, 'pza', 0.75, 'A-1', 1),
    (1, 'TOR-003', 'Tornillo M5x20',      'Cabeza hexagonal, grado 8.8',          200, 100, 'pza', 1.00, 'A-2', 1),
    (1, 'TOR-004', 'Tuerca M3',           'Hexagonal, acero inoxidable',            45,  50, 'pza', 0.30, 'A-1', 1),
    (1, 'TOR-005', 'Arandela M3',         'Plana, acero inoxidable',               500, 100, 'pza', 0.10, 'A-3', 1),
    (1, 'TOR-006', 'Tornillo Allen M6x25', 'Socket cap, negro grado 12.9',          80,  20, 'pza', 1.50, 'A-2', 1),
    (1, 'TOR-007', 'Perno M8x30',         'Hexagonal con tuerca, grado 8.8',        10,  25, 'pza', 2.00, 'B-1', 1),
    (1, 'TOR-008', 'Rondana plana M4',    'Galvanizada, diametro ext 12mm',        300,  50, 'pza', 0.15, 'A-1', 1),
    (1, 'TOR-009', 'Tornillo M6x30',      'Cabeza hexagonal, galvanizado',         120,  40, 'pza', 1.20, 'A-2', 1),
    (1, 'TOR-010', 'Prisionero M5x8',     'Allen sin cabeza, punta plana',          60,  30, 'pza', 0.80, 'A-3', 1);

-- CABLEADO (cat 2) - 8 productos
INSERT INTO inventory_products (category_id, code, name, description, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (2, 'CAB-001', 'Cable THW 12 AWG',    'Rojo, 600V, CONDUMEX',                   85,  50, 'm',   8.50, 'B-1', 1),
    (2, 'CAB-002', 'Cable THW 14 AWG',    'Negro, 600V, CONDUMEX',                  120,  50, 'm',   6.20, 'B-1', 1),
    (2, 'CAB-003', 'Cable multiconductor 4x18', '4 hilos, blindado, para control',    30,  20, 'm',  22.00, 'B-2', 1),
    (2, 'CAB-004', 'Cable UTP Cat6',      'Azul, 305m bobina, Panduit',              180,  50, 'm',   7.50, 'B-2', 1),
    (2, 'CAB-005', 'Cable uso rudo 3x12', '3 conductores, 600V, exterior',            15,  10, 'm',  35.00, 'B-3', 1),
    (2, 'CAB-006', 'Cable sensor M12',    '4 pines, PVC, 2 metros',                   25,  15, 'pza', 85.00, 'B-2', 1),
    (2, 'CAB-007', 'Termopar tipo K',     'Cable compensado, 2x20 AWG',               40,  20, 'm',  18.00, 'B-3', 1),
    (2, 'CAB-008', 'Cable flexible 18 AWG', 'Blanco, para tablero de control',       200, 100, 'm',   4.50, 'B-1', 1);

-- CONECTORES (cat 3) - 8 productos
INSERT INTO inventory_products (category_id, code, name, description, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (3, 'CON-001', 'Conector M12 macho',    '4 pines, IP67, recto',                   18,  20, 'pza', 120.00, 'C-1', 1),
    (3, 'CON-002', 'Conector M12 hembra',   '4 pines, IP67, recto',                   12,  20, 'pza', 125.00, 'C-1', 1),
    (3, 'CON-003', 'Terminal punta hueca',   'Roja, 1.0mm2, bolsa 100',               350, 200, 'pza',   0.40, 'C-2', 1),
    (3, 'CON-004', 'Terminal tipo ojo M4',   'Azul, 1.5-2.5mm2',                      280, 100, 'pza',   0.55, 'C-2', 1),
    (3, 'CON-005', 'Borne de riel DIN',     '2.5mm2, gris, Phoenix Contact',           45,  30, 'pza',  28.00, 'C-3', 1),
    (3, 'CON-006', 'Borne de tierra',       '2.5mm2, verde/amarillo, Phoenix',          20,  15, 'pza',  32.00, 'C-3', 1),
    (3, 'CON-007', 'Conector DB9 macho',    'Soldable, con carcasa metalica',           15,  10, 'pza',  35.00, 'C-1', 1),
    (3, 'CON-008', 'Conector RJ45',         'Cat6, blindado, crimpar',                  80,  50, 'pza',  12.00, 'C-2', 1);

-- HERRAMIENTAS (cat 4) - 6 productos
INSERT INTO inventory_products (category_id, code, name, description, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (4, 'HER-001', 'Desarmador Phillips #2', 'Mango ergonomico, Stanley',               8,   5, 'pza',  95.00, 'D-1', 1),
    (4, 'HER-002', 'Pinza pelacables',       'Automatica, 0.5-6mm2, Knipex',            3,   2, 'pza', 450.00, 'D-1', 1),
    (4, 'HER-003', 'Multimetro digital',     'Fluke 117, True RMS',                     2,   2, 'pza', 4200.00, 'D-2', 1),
    (4, 'HER-004', 'Juego llaves Allen',     'Metricas 1.5-10mm, Bondhus',              5,   3, 'pza', 280.00, 'D-1', 1),
    (4, 'HER-005', 'Crimpeadora M12',        'Para conectores industriales',             1,   1, 'pza', 1800.00, 'D-2', 1),
    (4, 'HER-006', 'Nivel digital',          '30cm, precision 0.05deg',                  2,   1, 'pza', 650.00, 'D-2', 1);

-- SENSORES (cat 5) - 7 productos
INSERT INTO inventory_products (category_id, code, name, description, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (5, 'SEN-001', 'Sensor inductivo M12',   'PNP NO, 4mm, 10-30VDC, Sick',            8,  10, 'pza', 380.00, 'E-1', 1),
    (5, 'SEN-002', 'Sensor inductivo M18',   'PNP NO, 8mm, 10-30VDC, Omron',          12,  10, 'pza', 320.00, 'E-1', 1),
    (5, 'SEN-003', 'Sensor fotoelectrico',   'Difuso, 100mm, M12, Banner',              4,   5, 'pza', 850.00, 'E-2', 1),
    (5, 'SEN-004', 'Termopar tipo K',        'Bayoneta, 100mm, 0-400C',                 6,   5, 'pza', 220.00, 'E-2', 1),
    (5, 'SEN-005', 'Sensor de presion',      '0-10bar, 4-20mA, rosca 1/4NPT',          3,   3, 'pza', 1200.00, 'E-3', 1),
    (5, 'SEN-006', 'Encoder incremental',    '1000 PPR, eje 8mm, Autonics',             2,   2, 'pza', 1500.00, 'E-3', 1),
    (5, 'SEN-007', 'Switch de limite',       'Rodillo, 1NA+1NC, Schneider',            15,  10, 'pza', 150.00, 'E-1', 1);

-- MOTORES (cat 6) - 6 productos
INSERT INTO inventory_products (category_id, code, name, description, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (6, 'MOT-001', 'Motor paso a paso NEMA17', '1.8deg, 1.5A, 4 hilos',                5,   3, 'pza', 280.00, 'F-1', 1),
    (6, 'MOT-002', 'Motor paso a paso NEMA23', '1.8deg, 3A, 4 hilos',                  3,   2, 'pza', 650.00, 'F-1', 1),
    (6, 'MOT-003', 'Servomotor AC 750W',       'Mitsubishi HG-KR73, con encoder',       1,   1, 'pza', 8500.00, 'F-2', 1),
    (6, 'MOT-004', 'Motor DC 24V 100W',        'Con reductor 1:30, eje 8mm',            4,   2, 'pza', 950.00, 'F-1', 1),
    (6, 'MOT-005', 'Motor trifasico 1HP',      '1750RPM, 220/440V, brida C',            2,   1, 'pza', 3200.00, 'F-2', 1),
    (6, 'MOT-006', 'Ventilador axial 120mm',   '24VDC, 120x120x38mm, rodamiento',      10,   5, 'pza', 180.00, 'F-1', 1);

-- NEUMATICA (cat 7) - 7 productos
INSERT INTO inventory_products (category_id, code, name, description, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (7, 'NEU-001', 'Cilindro doble efecto',    'Bore 32mm, carrera 100mm, Festo',       4,   3, 'pza', 1400.00, 'G-1', 1),
    (7, 'NEU-002', 'Cilindro compacto',        'Bore 20mm, carrera 50mm, SMC',          6,   4, 'pza', 680.00, 'G-1', 1),
    (7, 'NEU-003', 'Valvula 5/2 solenoide',    '24VDC, 1/4NPT, Festo',                 5,   3, 'pza', 950.00, 'G-2', 1),
    (7, 'NEU-004', 'Regulador de presion',      '1/4NPT, 0-10bar, con manometro',       3,   2, 'pza', 380.00, 'G-2', 1),
    (7, 'NEU-005', 'Conector rapido 6mm',       'Recto, rosca 1/4NPT',                 40,  30, 'pza',  25.00, 'G-3', 1),
    (7, 'NEU-006', 'Manguera PU 6mm',          'Azul, poliuretano, rollo 100m',        60,  30, 'm',    8.00, 'G-3', 1),
    (7, 'NEU-007', 'Silenciador bronce',       '1/4NPT, sinterizado',                   20,  15, 'pza',  18.00, 'G-3', 1);

-- ELECTRONICA (cat 8) - 8 productos
INSERT INTO inventory_products (category_id, code, name, description, stock_current, stock_minimum, unit, unit_price, location, created_by) VALUES
    (8, 'ELE-001', 'PLC Siemens S7-1200',     'CPU 1214C, 14DI/10DO/2AI',              1,   1, 'pza', 12000.00, 'H-1', 1),
    (8, 'ELE-002', 'Fuente 24VDC 5A',         'Riel DIN, Mean Well, 120W',              4,   3, 'pza', 650.00, 'H-2', 1),
    (8, 'ELE-003', 'Relevador 24VDC',         '2 contactos, 10A, con base, Omron',     15,  10, 'pza', 180.00, 'H-2', 1),
    (8, 'ELE-004', 'Interruptor termomagnetico', '2P, 20A, Schneider',                  6,   4, 'pza', 320.00, 'H-3', 1),
    (8, 'ELE-005', 'Contactor AC 18A',        '3P, bobina 24VDC, Schneider',            3,   2, 'pza', 580.00, 'H-3', 1),
    (8, 'ELE-006', 'HMI 7 pulgadas',          'Siemens KTP700, touch, Ethernet',        1,   1, 'pza', 15000.00, 'H-1', 1),
    (8, 'ELE-007', 'Variador de frecuencia',  '1HP, 220V, Siemens V20',                 2,   1, 'pza', 4800.00, 'H-1', 1),
    (8, 'ELE-008', 'Boton pulsador 22mm',     'Verde, rasante, 1NA, Schneider',        20,  10, 'pza',  85.00, 'H-3', 1);

-- ============================================
-- VERIFICACION
-- ============================================
SELECT '--- RESUMEN ---' AS info;
SELECT name AS categoria, total_products, low_stock_count, total_stock, total_value, health_percent
FROM v_inventory_category_summary;

SELECT '--- GLOBAL ---' AS info;
SELECT fn_get_inventory_stats();

SELECT '--- PRODUCTOS CON STOCK BAJO ---' AS info;
SELECT category_name, code, name, stock_current, stock_minimum, cantidad_por_pedir
FROM v_inventory_low_stock;
