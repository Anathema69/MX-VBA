import { Search, Plus, Package, AlertTriangle } from 'lucide-react';
import type { Category } from '../App';

type InventoryHomeProps = {
  onCategoryClick: (category: Category) => void;
};

const categories: Category[] = [
  { id: 1, name: 'TORNILLERÍA', description: 'Tornillos, tuercas y arandelas', color: '#3B82F6', productsCount: 42, lowStockCount: 3 },
  { id: 2, name: 'CABLEADO', description: 'Cables eléctricos y de datos', color: '#10B981', productsCount: 18, lowStockCount: 0 },
  { id: 3, name: 'CONECTORES', description: 'Conectores industriales y terminales', color: '#8B5CF6', productsCount: 25, lowStockCount: 7 },
  { id: 4, name: 'HERRAMIENTAS', description: 'Herramientas manuales y eléctricas', color: '#F59E0B', productsCount: 15, lowStockCount: 1 },
  { id: 5, name: 'SENSORES', description: 'Sensores de proximidad, temperatura', color: '#EC4899', productsCount: 8, lowStockCount: 2 },
  { id: 6, name: 'MOTORES', description: 'Motores AC, DC y paso a paso', color: '#EF4444', productsCount: 12, lowStockCount: 0 },
];

export default function InventoryHome({ onCategoryClick }: InventoryHomeProps) {
  const totalProducts = categories.reduce((sum, cat) => sum + cat.productsCount, 0);
  const totalLowStock = categories.reduce((sum, cat) => sum + cat.lowStockCount, 0);

  return (
    <div className="w-full h-full flex flex-col overflow-hidden" style={{ backgroundColor: '#FFFFFF', borderRadius: '0' }}>
      {/* Header */}
      <div className="flex-none" style={{ borderBottom: '1px solid #E2E8F0', padding: '20px 24px' }}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            {/* Logo Icon */}
            <div 
              className="flex items-center justify-center" 
              style={{ 
                width: '48px', 
                height: '48px', 
                borderRadius: '12px',
                background: 'linear-gradient(135deg, #1D4ED8 0%, #1E3A8A 100%)',
              }}
            >
              <Package className="text-white" size={28} strokeWidth={2} />
            </div>
            
            <div>
              <div style={{ fontSize: '24px', fontWeight: '700', color: '#0F172A', lineHeight: '1.2', fontFamily: 'Segoe UI, sans-serif' }}>
                INVENTARIO
              </div>
              <div style={{ fontSize: '14px', color: '#64748B', marginTop: '2px', fontFamily: 'Segoe UI, sans-serif' }}>
                Gestión de componentes
              </div>
            </div>
          </div>

          <button
            className="flex items-center gap-2 px-4 py-2 bg-white hover:bg-gray-50 transition-colors"
            style={{ 
              border: '1px solid #E2E8F0', 
              borderRadius: '8px',
              fontSize: '13px',
              color: '#0F172A',
              fontFamily: 'Segoe UI, sans-serif'
            }}
          >
            ← Volver
          </button>
        </div>
      </div>

      {/* Action Bar */}
      <div 
        className="flex-none flex items-center justify-between" 
        style={{ 
          backgroundColor: '#F8FAFC', 
          padding: '16px 24px',
          borderBottom: '1px solid #F1F5F9'
        }}
      >
        <div className="relative" style={{ width: '300px' }}>
          <Search 
            className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" 
            size={18} 
          />
          <input
            type="text"
            placeholder="Buscar categoría..."
            className="w-full pl-10 pr-4 py-2 bg-white"
            style={{
              border: '1px solid #E2E8F0',
              borderRadius: '8px',
              fontSize: '13px',
              color: '#0F172A',
              fontFamily: 'Segoe UI, sans-serif',
              outline: 'none'
            }}
          />
        </div>

        <div className="flex items-center gap-3">
          <div 
            className="flex items-center gap-2 px-4 py-2"
            style={{ 
              backgroundColor: '#EFF6FF', 
              borderRadius: '20px',
              fontSize: '13px',
              color: '#1D4ED8',
              fontFamily: 'Segoe UI, sans-serif'
            }}
          >
            📦 {totalProducts} productos
          </div>
          
          <div 
            className="flex items-center gap-2 px-4 py-2"
            style={{ 
              backgroundColor: '#FFFBEB', 
              borderRadius: '20px',
              fontSize: '13px',
              color: '#D97706',
              fontFamily: 'Segoe UI, sans-serif'
            }}
          >
            ⚠️ {totalLowStock} por pedir
          </div>
        </div>

        <button
          className="flex items-center gap-2 px-4 py-2 hover:opacity-90 transition-opacity"
          style={{ 
            backgroundColor: '#1D4ED8', 
            color: 'white',
            borderRadius: '8px',
            fontSize: '13px',
            fontWeight: '700',
            fontFamily: 'Segoe UI, sans-serif'
          }}
        >
          <Plus size={18} strokeWidth={2.5} />
          Nueva Categoría
        </button>
      </div>

      {/* Content Grid */}
      <div 
        className="flex-1 overflow-y-auto" 
        style={{ 
          backgroundColor: '#F8FAFC', 
          padding: '24px'
        }}
      >
        <div className="grid grid-cols-3 gap-4">
          {categories.map((category) => (
            <div
              key={category.id}
              onClick={() => onCategoryClick(category)}
              className="bg-white hover:shadow-xl transition-all duration-200 cursor-pointer group relative"
              style={{
                borderRadius: '12px',
                boxShadow: '0 4px 16px rgba(30, 41, 59, 0.08)',
                padding: '20px',
                borderLeft: `4px solid ${category.color}`,
              }}
            >
              {/* Category Name */}
              <div style={{ fontSize: '16px', fontWeight: '600', color: '#0F172A', marginBottom: '4px', fontFamily: 'Segoe UI, sans-serif' }}>
                {category.name}
              </div>
              
              {/* Description */}
              <div 
                style={{ 
                  fontSize: '12px', 
                  color: '#64748B', 
                  marginBottom: '16px',
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  fontFamily: 'Segoe UI, sans-serif'
                }}
              >
                {category.description}
              </div>

              {/* Separator */}
              <div style={{ height: '1px', backgroundColor: '#F1F5F9', marginBottom: '16px' }}></div>

              {/* Stats */}
              <div className="grid grid-cols-2 gap-4 mb-4">
                <div>
                  <div style={{ fontSize: '11px', color: '#94A3B8', marginBottom: '4px', fontFamily: 'Segoe UI, sans-serif' }}>
                    Productos
                  </div>
                  <div style={{ fontSize: '24px', fontWeight: '700', color: '#0F172A', fontFamily: 'Segoe UI, sans-serif' }}>
                    {category.productsCount}
                  </div>
                </div>
                <div>
                  <div style={{ fontSize: '11px', color: '#94A3B8', marginBottom: '4px', fontFamily: 'Segoe UI, sans-serif' }}>
                    Stock total
                  </div>
                  <div style={{ fontSize: '24px', fontWeight: '700', color: '#0F172A', fontFamily: 'Segoe UI, sans-serif' }}>
                    {Math.floor(Math.random() * 1000) + 100}
                  </div>
                </div>
              </div>

              {/* Badge */}
              {category.lowStockCount > 0 ? (
                <div 
                  className="inline-flex items-center gap-1 px-3 py-1"
                  style={{ 
                    backgroundColor: '#FFFBEB', 
                    color: '#D97706',
                    borderRadius: '20px',
                    fontSize: '12px',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  <AlertTriangle size={12} />
                  {category.lowStockCount} por pedir
                </div>
              ) : (
                <div 
                  className="inline-flex items-center gap-1 px-3 py-1"
                  style={{ 
                    backgroundColor: '#F0FFF4', 
                    color: '#48BB78',
                    borderRadius: '20px',
                    fontSize: '12px',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  ✓ Stock OK
                </div>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Footer */}
      <div 
        className="flex-none flex items-center justify-between" 
        style={{ 
          borderTop: '1px solid #E2E8F0', 
          height: '36px',
          padding: '0 24px',
          backgroundColor: '#FFFFFF'
        }}
      >
        <div style={{ fontSize: '12px', color: '#64748B', fontFamily: 'Segoe UI, sans-serif' }}>
          {categories.length} categorías activas
        </div>
        <div style={{ fontSize: '12px', color: '#94A3B8', fontFamily: 'Segoe UI, sans-serif' }}>
          Última actualización: 13/03/2026 14:30
        </div>
      </div>
    </div>
  );
}
